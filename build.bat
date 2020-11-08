@echo off
setlocal
pushd %~dp0

set PLAT="x64"
:parse
if /i "%1" EQU "x64" (set PLAT=x64 & goto :continue)
if /i "%1" EQU "x86" (set PLAT=x86 & goto :continue)
if "%1" NEQ "" goto :help

:continue

echo NOTE: Native library projects ignore the platform parameter. Both x86 and x64 binaries will be produced if supported by the running host.

call %~dp0Native\build-cnsock-win.bat
if %errorlevel% neq 0 goto error 

call %~dp0Native\build-replxx-win.bat
if %errorlevel% neq 0 goto error 

dotnet build -c Release A9.sln /p:Platform=%PLAT%
if %errorlevel% neq 0 goto error

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%

:help 
echo %0 ^[x86^|x64^]
