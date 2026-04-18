// XPC client used by WinPartFlash.Gui to trigger the
// com.hcgstudio.winpartflash.helper LaunchDaemon.  Lives in the library
// rather than in C# because setting an event handler on an
// xpc_connection_t requires an Objective-C block, which is awkward to
// construct from managed code.
//
// Only compiled on Apple platforms; elsewhere it's a no-op.

#include <stdint.h>
#include <stdio.h>
#include <string.h>

#if __APPLE__
#include <xpc/xpc.h>
#include <dispatch/dispatch.h>

// Trigger the helper.  Sends one xpc message carrying the connect-back
// parameters (device, op, offset, length, socket_path, token), waits for
// a reply dict, copies the helper's status code into *out_status_code,
// and copies the reply "error" string (if any) into err_buf.
//
// Returns 0 on a completed round-trip (caller must still check
// *out_status_code), non-zero on transport-level failure.
int wpf_xpc_send_trigger(
    const char* service_name,
    const char* device,
    const char* op,
    uint64_t offset,
    uint64_t length,
    const char* socket_path,
    const char* token,
    int64_t* out_status_code,
    char* err_buf,
    size_t err_buf_len)
{
    if (!service_name || !device || !op || !socket_path || !token || !out_status_code) return -1;
    *out_status_code = 0;
    if (err_buf && err_buf_len) err_buf[0] = '\0';

    xpc_connection_t conn = xpc_connection_create_mach_service(
        service_name, NULL, XPC_CONNECTION_MACH_SERVICE_PRIVILEGED);
    if (!conn) return -2;

    xpc_connection_set_event_handler(conn, ^(xpc_object_t event) {
        // Transient connection; we drive everything from
        // send_message_with_reply_sync, so no per-event work is needed.
        (void)event;
    });
    xpc_connection_resume(conn);

    xpc_object_t msg = xpc_dictionary_create(NULL, NULL, 0);
    xpc_dictionary_set_string(msg, "device", device);
    xpc_dictionary_set_string(msg, "op", op);
    xpc_dictionary_set_uint64(msg, "offset", offset);
    xpc_dictionary_set_uint64(msg, "length", length);
    xpc_dictionary_set_string(msg, "socket_path", socket_path);
    xpc_dictionary_set_string(msg, "token", token);

    xpc_object_t reply = xpc_connection_send_message_with_reply_sync(conn, msg);
    int rc = 0;

    xpc_type_t ty = xpc_get_type(reply);
    if (ty == XPC_TYPE_ERROR) {
        const char* desc = xpc_dictionary_get_string(reply, XPC_ERROR_KEY_DESCRIPTION);
        if (err_buf && err_buf_len && desc) {
            strncpy(err_buf, desc, err_buf_len - 1);
            err_buf[err_buf_len - 1] = '\0';
        }
        rc = -3;
    } else if (ty == XPC_TYPE_DICTIONARY) {
        *out_status_code = xpc_dictionary_get_int64(reply, "code");
        const char* err = xpc_dictionary_get_string(reply, "error");
        if (err && err_buf && err_buf_len) {
            strncpy(err_buf, err, err_buf_len - 1);
            err_buf[err_buf_len - 1] = '\0';
        }
    } else {
        rc = -4;
    }

    xpc_release(msg);
    xpc_release(reply);
    xpc_connection_cancel(conn);
    xpc_release(conn);
    return rc;
}

#else
int wpf_xpc_send_trigger(
    const char* service_name,
    const char* device,
    const char* op,
    uint64_t offset,
    uint64_t length,
    const char* socket_path,
    const char* token,
    int64_t* out_status_code,
    char* err_buf,
    size_t err_buf_len)
{
    (void)service_name; (void)device; (void)op;
    (void)offset; (void)length; (void)socket_path; (void)token;
    (void)out_status_code; (void)err_buf; (void)err_buf_len;
    return -1;
}
#endif
