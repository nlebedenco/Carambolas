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
"Carambolas.Unity" \
"Carambolas.Unity.Replication" \
"Carambolas.Unity.Replication-Editor" \
"Carambolas.Unity-Editor" \
"Tests.Application.Carambolas.Net.Host" \
"Tests.Integration.Carambolas.Net" \
"Tests.Unit.Carambolas" \
"Tests.Unit.Carambolas.Net" \
"Tests.Unit.Carambolas.Security.Cryptography" \
"Tests.Unit.Carambolas.Security.Cryptography.NaCl" \
"UnityPackageManager.Carambolas" \
"UnityPackageManager.Carambolas.Net" \
"UnityPackageManager.Carambolas.Unity" \
"UnityPackageManager.Carambolas.Unity.Replication" \
"UnityPackageManager.System.Memory"
do 
    dotnet build $P -c Release -p:Platform=$PLAT || exit 1
done


