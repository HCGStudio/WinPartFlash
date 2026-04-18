cmake_minimum_required(VERSION 3.28)
project(WinPartFlashHelper C)

set(CMAKE_C_STANDARD 11)

add_executable(WinPartFlashHelper helper.c)

if(APPLE)
    set_target_properties(WinPartFlashHelper PROPERTIES
        OUTPUT_NAME "com.hcgstudio.winpartflash.helper")
endif()
