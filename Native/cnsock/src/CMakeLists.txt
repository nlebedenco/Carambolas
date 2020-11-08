set(CMAKE_VERBOSE_MAKEFILE ON)
set(LIBNAME "cnsock")

add_library(${LIBNAME} SHARED cnsock.c resource.rc ${SOURCES})

if(WIN32)    
    target_link_libraries(${LIBNAME} winmm ws2_32)
endif()

install(TARGETS ${LIBNAME} DESTINATION)

# headers
install( 
    FILES cnsock.h 
    DESTINATION ${CMAKE_INSTALL_PREFIX}/include
)
