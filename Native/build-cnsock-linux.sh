#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )/cnsock"

BUILD_DIR_x86="$BASEDIR/../../Build/Native/cnsock/Release/linux-x86/cmake"
BUILD_DIR_x64="$BASEDIR/../../Build/Native/cnsock/Release/linux-x64/cmake"

mkdir -p $BUILD_DIR_x86 && cd $BUILD_DIR_x86 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS="-m32" -DCMAKE_INSTALL_PREFIX="../../linux-x86" \
&& cmake --build $BUILD_DIR_x86 --target install \
&& cp -v $BUILD_DIR_x86/../lib/libcnsock.so $BASEDIR/../../Carambolas.Net/Native/linux-x86/ \
|| exit 1

echo

mkdir -p $BUILD_DIR_x64 && cd $BUILD_DIR_x64 && \
cmake $BASEDIR -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS="-m64" -DCMAKE_INSTALL_PREFIX="../../linux-x64" \
&& cmake --build $BUILD_DIR_x64 --target install \
&& cp -v $BUILD_DIR_x64/../lib/libcnsock.so $BASEDIR/../../Carambolas.Net/Native/linux-x64/ \
|| exit 1
