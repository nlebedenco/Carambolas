#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

for P in \
"Carambolas.Core" \
"Carambolas.Net" \
"Carambolas.Replxx" \
"Carambolas.Runtime.InteropServices"
do 
  dotnet pack $P -c Release -p:Platform=AnyCPU --output "$BASEDIR/Build/NuGet" || exit 1
done

