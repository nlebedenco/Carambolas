#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )/cnsock"

BUILD_DIR_x64="$BASEDIR/../../Build/Native/cnsock/Release/osx-x64/cmake"

mkdir -p $BUILD_DIR_x64 && cd $BUILD_DIR_x64 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="../../osx-x64" \
&& cmake --build $BUILD_DIR_x64 --target install \
&& cp -v $BUILD_DIR_x64/../lib/libcnsock.dylib $BASEDIR/../../Carambolas.Net/Native/osx-x64/ \
|| exit 1
