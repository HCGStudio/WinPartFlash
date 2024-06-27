#include <stdio.h>

#include "library.h"

int main()
{
    uint64_t sectors = 0;
    int result = getBlockSize("/dev/disk4", &sectors);
    printf("%llu, %d\n", sectors, result);
    return 0;
}
