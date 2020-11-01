@echo off
setlocal
pushd %~dp0

set PLAT="x64"
:parse
if /i "%1" EQU "x64" (set PLAT="x64" & goto :continue)
if /i "%1" EQU "x86" (set PLAT="x86" & goto :continue)
if "%1" NEQ "" goto :help

:continue
call %~dp0Carambolas.Net.Native\build-win.bat
if %errorlevel% neq 0 goto error 

for %%P in (
    "Carambolas" 
    "Carambolas.Net" 
    "Carambolas.Unity" 
    "Carambolas.Unity.Replication" 
    "Carambolas.Unity.Replication-Editor" 
    "Carambolas.Unity-Editor"         
    "Tests.Application.Carambolas.Net.Host" 
    "Tests.Integration.Carambolas.Net" 
    "Tests.Unit.Carambolas" 
    "Tests.Unit.Carambolas.Net" 
    "Tests.Unit.Carambolas.Security.Cryptography" 
    "Tests.Unit.Carambolas.Security.Cryptography.NaCl" 
    "UnityPackageManager.Carambolas"
    "UnityPackageManager.Carambolas.Net"
    "UnityPackageManager.Carambolas.Unity"
    "UnityPackageManager.Carambolas.Unity.Replication"
    "UnityPackageManager.System.Memory"
) do (
    dotnet build %%P -c Release -p:Platform=%PLAT% 
    if %errorlevel% neq 0 goto error
)

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%

:help 
echo %0 ^[x86^|x64^]
