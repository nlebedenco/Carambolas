#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"
PLAT=${1:-x64}

if [ $PLAT != "x64" ] && [ $PLAT != "x86" ]; then 
echo $0 \[x86\|x64\]
exit 1 
fi

echo "NOTE: Native library projects ignore the platform parameter. Both x86 and x64 binaries will be produced if supported by the running host."

: ${OSTYPE:=$(uname -s |tr '[:upper:]' '[:lower:]')}

case "$OSTYPE" in
  linux*) 
    "$BASEDIR/Native/build-cnsock-linux.sh" || exit 1
    "$BASEDIR/Native/build-replxx-linux.sh" || exit 1  
  ;;
  darwin*) 
    "$BASEDIR/Native/build-cnsock-osx.sh" || exit 1 
    "$BASEDIR/Native/build-replxx-osx.sh" || exit 1 
  ;;
  *) echo Current platform is not supported by this build script: $OSTYPE; exit 1 ;;
esac

dotnet build -c Release A9.sln /p:Platform=$PLAT || exit 1


