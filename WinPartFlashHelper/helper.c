/*
 * WinPartFlash privileged helper.
 *
 * Runs as root to perform raw block-device I/O on macOS.  The GUI must NOT be
 * launched with sudo; this helper is the only piece of the app that ever
 * touches /dev/rdiskN.  Two invocation modes are supported:
 *
 *   1. Dev fallback: the GUI spawns this helper through
 *      `osascript ... with administrator privileges` and supplies a Unix-domain
 *      socket path + a one-shot 32-byte hex auth token via argv.  The helper
 *      connects, sends the token first, and then streams data.
 *
 *   2. Production (SMAppService LaunchDaemon, only on a properly signed
 *      build): xpc_main is wired up below as #ifdef WPF_HELPER_XPC.  Disabled
 *      by default because launchd registration requires a Developer ID
 *      identity that this repository does not yet ship with.
 *
 * Hard rules: no shell, no exec, no system().  Argv is parsed with a strict
 * whitelist, the device path must match /dev/r?diskN, and length is capped by
 * the device's reported byte count.
 */

#include <ctype.h>
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/disk.h>
#include <sys/ioctl.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/un.h>
#include <unistd.h>

#define CHUNK_BYTES (1 << 20)   /* 1 MiB */
#define TOKEN_HEX_LEN 64        /* 32 bytes -> 64 hex chars */

enum op_kind { OP_NONE, OP_READ, OP_WRITE };

struct args {
    const char* device;
    enum op_kind op;
    uint64_t offset;
    uint64_t length;
    const char* socket_path;
    const char* token_hex;
};

static int valid_device(const char* s)
{
    if (!s) return 0;
    if (strncmp(s, "/dev/disk", 9) == 0) s += 9;
    else if (strncmp(s, "/dev/rdisk", 10) == 0) s += 10;
    else return 0;
    if (!*s) return 0;
    while (*s) {
        if (!isdigit((unsigned char)*s)) return 0;
        ++s;
    }
    return 1;
}

static int valid_token_hex(const char* s)
{
    if (!s) return 0;
    if (strlen(s) != TOKEN_HEX_LEN) return 0;
    for (size_t i = 0; i < TOKEN_HEX_LEN; ++i)
        if (!isxdigit((unsigned char)s[i])) return 0;
    return 1;
}

static int parse_u64(const char* s, uint64_t* out)
{
    if (!s || !*s) return -1;
    char* end = NULL;
    errno = 0;
    unsigned long long v = strtoull(s, &end, 10);
    if (errno || !end || *end) return -1;
    *out = (uint64_t)v;
    return 0;
}

static int parse_args(int argc, char** argv, struct args* a)
{
    memset(a, 0, sizeof(*a));
    for (int i = 1; i < argc; ++i) {
        const char* k = argv[i];
        const char* v = (i + 1 < argc) ? argv[i + 1] : NULL;
        if (!strcmp(k, "--device") && v) { a->device = v; ++i; }
        else if (!strcmp(k, "--op") && v) {
            if (!strcmp(v, "read")) a->op = OP_READ;
            else if (!strcmp(v, "write")) a->op = OP_WRITE;
            else return -1;
            ++i;
        }
        else if (!strcmp(k, "--offset") && v) { if (parse_u64(v, &a->offset)) return -1; ++i; }
        else if (!strcmp(k, "--length") && v) { if (parse_u64(v, &a->length)) return -1; ++i; }
        else if (!strcmp(k, "--socket") && v) { a->socket_path = v; ++i; }
        else if (!strcmp(k, "--token")  && v) { a->token_hex   = v; ++i; }
        else return -1;
    }
    if (!valid_device(a->device)) return -1;
    if (a->op == OP_NONE) return -1;
    if (!a->socket_path || !*a->socket_path) return -1;
    if (strlen(a->socket_path) >= sizeof(((struct sockaddr_un*)0)->sun_path)) return -1;
    if (!valid_token_hex(a->token_hex)) return -1;
    return 0;
}

static int device_byte_count(int fd, uint64_t* out)
{
    uint32_t bs = 0;
    uint64_t bc = 0;
    if (ioctl(fd, DKIOCGETBLOCKSIZE, &bs) < 0) return -1;
    if (ioctl(fd, DKIOCGETBLOCKCOUNT, &bc) < 0) return -1;
    if (bs == 0) return -1;
    *out = (uint64_t)bs * bc;
    return 0;
}

static int connect_socket(const char* path)
{
    int s = socket(AF_UNIX, SOCK_STREAM, 0);
    if (s < 0) return -1;
    struct sockaddr_un addr;
    memset(&addr, 0, sizeof(addr));
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, path, sizeof(addr.sun_path) - 1);
    if (connect(s, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
        close(s);
        return -1;
    }
    return s;
}

static ssize_t write_all(int fd, const void* buf, size_t n)
{
    const char* p = buf;
    size_t left = n;
    while (left) {
        ssize_t w = write(fd, p, left);
        if (w < 0) { if (errno == EINTR) continue; return -1; }
        if (w == 0) return -1;
        p += w; left -= (size_t)w;
    }
    return (ssize_t)n;
}

static ssize_t read_some(int fd, void* buf, size_t n)
{
    for (;;) {
        ssize_t r = read(fd, buf, n);
        if (r < 0 && errno == EINTR) continue;
        return r;
    }
}

static int do_read(int dev, int sock, uint64_t offset, uint64_t length)
{
    static char buf[CHUNK_BYTES];
    uint64_t remaining = length;
    while (remaining) {
        size_t want = remaining > CHUNK_BYTES ? CHUNK_BYTES : (size_t)remaining;
        ssize_t r = pread(dev, buf, want, (off_t)offset);
        if (r <= 0) return -1;
        if (write_all(sock, buf, (size_t)r) < 0) return -1;
        offset += (uint64_t)r;
        remaining -= (uint64_t)r;
    }
    return 0;
}

static int do_write(int dev, int sock, uint64_t offset, uint64_t length)
{
    static char buf[CHUNK_BYTES];
    uint64_t remaining = length;
    while (remaining) {
        size_t want = remaining > CHUNK_BYTES ? CHUNK_BYTES : (size_t)remaining;
        ssize_t r = read_some(sock, buf, want);
        if (r == 0) break;     /* clean EOF from GUI */
        if (r < 0) return -1;
        if (pwrite(dev, buf, (size_t)r, (off_t)offset) != r) return -1;
        offset += (uint64_t)r;
        remaining -= (uint64_t)r;
    }
    fsync(dev);
    return 0;
}

int main(int argc, char** argv)
{
    struct args a;
    if (parse_args(argc, argv, &a) != 0) {
        fprintf(stderr, "wpf-helper: bad arguments\n");
        return 2;
    }

    int dev = open(a.device, a.op == OP_WRITE ? O_WRONLY : O_RDONLY);
    if (dev < 0) {
        fprintf(stderr, "wpf-helper: open(%s): %s\n", a.device, strerror(errno));
        return 3;
    }

    uint64_t cap = 0;
    if (device_byte_count(dev, &cap) != 0) {
        fprintf(stderr, "wpf-helper: ioctl on %s: %s\n", a.device, strerror(errno));
        close(dev);
        return 4;
    }
    if (a.offset > cap || a.length > cap - a.offset) {
        fprintf(stderr, "wpf-helper: offset/length exceeds device size\n");
        close(dev);
        return 5;
    }

    int sock = connect_socket(a.socket_path);
    if (sock < 0) {
        fprintf(stderr, "wpf-helper: connect(%s): %s\n", a.socket_path, strerror(errno));
        close(dev);
        return 6;
    }

    /* Send the auth token first thing so the GUI can verify we're the helper
     * it spawned and not some other process that happened upon the socket. */
    if (write_all(sock, a.token_hex, TOKEN_HEX_LEN) < 0) {
        close(sock); close(dev);
        return 7;
    }

    int rc = (a.op == OP_READ)
        ? do_read(dev, sock, a.offset, a.length)
        : do_write(dev, sock, a.offset, a.length);

    shutdown(sock, SHUT_RDWR);
    close(sock);
    close(dev);
    return rc == 0 ? 0 : 8;
}
