#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    "$BASEDIR/Carambolas.Net.Native/build-linux.sh" || exit 1;
    dotnet pack Carambolas.Net.Native/nuget/Carambolas.Net.Native.Linux.csproj -c Release --output "$BASEDIR/Build/NuGet" || exit 1;
elif [[ "$OSTYPE" == "darwin"* ]]; then
    "$BASEDIR/Carambolas.Net.Native/build-osx.sh" || exit 1;
    dotnet pack Carambolas.Net.Native/nuget/Carambolas.Net.Native.macOS.csproj -c Release --output "$BASEDIR/Build/NuGet" || exit 1;
else 
    echo Current platform is not supported by this build script: $OSTYPE
    exit 1 
fi

# These assemblies are currently compiled for AnyCPU on both x86 and x64. 
# Passing a platform only to put files in the same output path used by the build scripts and visual studio.

dotnet pack Carambolas/Carambolas.csproj -c Release -p:Platform=x64 --output "$BASEDIR/Build/NuGet" || exit 1
dotnet pack Carambolas.Net/Carambolas.Net.csproj -c Release -p:Platform=x64 --output "$BASEDIR/Build/NuGet" || exit 1

