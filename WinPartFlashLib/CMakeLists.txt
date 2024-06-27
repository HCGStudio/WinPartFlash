cmake_minimum_required(VERSION 3.28)
project(WinPartFlashLib C)

set(CMAKE_C_STANDARD 11)

add_library(WinPartFlashLib SHARED library.c)

add_executable(WinPartFlashLibTest test.c)
target_link_libraries(WinPartFlashLibTest WinPartFlashLib)
