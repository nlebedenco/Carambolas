#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )/replxx"

BUILD_DIR_x64="$BASEDIR/../../Build/Native/replxx/Release/cmake/osx-x64"

mkdir -p $BUILD_DIR_x64 && cd $BUILD_DIR_x64 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="../../bin/osx-x64" -DBUILD_SHARED_LIBS=ON -DREPLXX_BUILD_EXAMPLES=OFF \
&& cmake --build $BUILD_DIR_x64 --target install
