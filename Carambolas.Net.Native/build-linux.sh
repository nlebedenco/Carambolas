#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

BUILD_DIR_x86="$BASEDIR/../Build/CMake/linux-x86"
BUILD_DIR_x64="$BASEDIR/../Build/CMake/linux-x64"

mkdir -p $BUILD_DIR_x86 && cd $BUILD_DIR_x86 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS="-m32" -DCMAKE_INSTALL_PREFIX="../../Native/runtimes/linux-x86" \
&& cmake --build $BUILD_DIR_x86 --target install

mkdir -p $BUILD_DIR_x64 && cd $BUILD_DIR_x64 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS="-m64" -DCMAKE_INSTALL_PREFIX="../../Native/runtimes/linux-x64" \
&& cmake --build $BUILD_DIR_x64 --target install
