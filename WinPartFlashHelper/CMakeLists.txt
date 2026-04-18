cmake_minimum_required(VERSION 3.28)
project(WinPartFlashHelper C)

set(CMAKE_C_STANDARD 11)

add_executable(WinPartFlashHelper helper.c)

if(APPLE)
    set_target_properties(WinPartFlashHelper PROPERTIES
        OUTPUT_NAME "com.hcgstudio.winpartflash.helper")
    target_compile_definitions(WinPartFlashHelper PRIVATE WPF_HELPER_XPC=1)
    # libxpc + dispatch are part of libSystem on macOS; linking the
    # executable normally is sufficient.  Enable blocks syntax explicitly
    # so `^(...)` compiles under non-default toolchains.
    target_compile_options(WinPartFlashHelper PRIVATE -fblocks)
endif()
