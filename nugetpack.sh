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

dotnet pack Carambolas/Carambolas.csproj -c Release --output "$BASEDIR/Build/NuGet" || exit 1
dotnet pack Carambolas.Net/Carambolas.Net.csproj -c Release --output "$BASEDIR/Build/NuGet" || exit 1

