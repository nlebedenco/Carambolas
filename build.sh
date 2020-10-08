#!/bin/sh

BASEDIR="$( cd "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"
PLAT=${1:-x64}

if [ $PLAT != "x64" ] && [ $PLAT != "x86" ]; then 
echo $0 \[x86\|x64\]
exit 1 
fi

: ${OSTYPE:=$(uname -s |tr '[:upper:]' '[:lower:]')}

case "$OSTYPE" in
  linux*) "$BASEDIR/Carambolas.Net.Native/build-linux.sh" || exit 1 ;;
  darwin*) "$BASEDIR/Carambolas.Net.Native/build-osx.sh" || exit 1 ;;
  *) echo Current platform is not supported by this build script: $OSTYPE; exit 1 ;;
esac

for P in \
"Carambolas" \
"Carambolas.Net" \
"Carambolas.Tests" \
"Carambolas.Net.Tests" \
"Carambolas.Net.Tests.Host" \
"Carambolas.Net.Tests.Integration" \
"Carambolas.Security.Cryptography.Tests" \
"Carambolas.Security.Cryptography.NaCl.Tests"
do 
    dotnet build $P -c Release -p:Platform=$PLAT || exit 1
done


