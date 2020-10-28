#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

BUILD_DIR_x64="$BASEDIR/../Build/CMake/osx-x64"

mkdir -p $BUILD_DIR_x64 && cd $BUILD_DIR_x64 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="../../Native/runtimes/osx-x64" \
&& cmake --build $BUILD_DIR_x64 --target install
