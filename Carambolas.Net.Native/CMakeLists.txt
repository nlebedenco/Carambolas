cmake_minimum_required (VERSION 2.8)
set(CMAKE_VERBOSE_MAKEFILE ON)

project("Carambolas.Net.Native" C)
    
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()

if(UNIX AND NOT APPLE)
    set(LINUX TRUE)
endif()

if(MSVC)
	add_definitions(-W3)
else()
	add_definitions(-Wno-error)
endif()

if(APPLE)
	add_link_options(-Wl,-x)
endif()

if(LINUX)
	add_link_options(-Wl,-s)
endif()

add_subdirectory(${PROJECT_SOURCE_DIR}/src)
