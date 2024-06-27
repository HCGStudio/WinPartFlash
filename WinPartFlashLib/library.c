#include "library.h"

#include <sys/disk.h>
#include <sys/ioctl.h>
#include <unistd.h>
#include <fcntl.h>


int getBlockSize(const char* name, uint64_t* size)
{
    int dev = open(name, O_RDONLY);
    if(!dev || dev == -1)
        return -1;

    uint64_t sectors = 0;
    ioctl(dev, DKIOCGETBLOCKSIZE, &sectors);

    *size = sectors;

    close(dev);
    return 0;
}
