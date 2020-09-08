set(CMAKE_VERBOSE_MAKEFILE ON)

if(WIN32)
    set(LIBNAME "Carambolas.Net.Native")
else()
    set(LIBNAME "Carambolas.Net.Native.dll")
endif()

add_library(${LIBNAME} SHARED native.c resource.rc ${SOURCES})

if(WIN32)    
    target_link_libraries(${LIBNAME} winmm ws2_32)
endif()

install(TARGETS ${LIBNAME} DESTINATION native)