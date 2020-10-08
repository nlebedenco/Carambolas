@echo off
setlocal
pushd %~dp0

call %~dp0Carambolas.Net.Native\build-win.bat
if %errorlevel% neq 0 goto error 

dotnet pack Carambolas.Net.Native\nuget\Carambolas.Net.Native.Win.csproj -c Release --output "%~dp0Build\NuGet"
if %errorlevel% neq 0 goto error 

REM These assemblies are currently compiled for AnyCPU on both x86 and x64. 
REM Passing a platform only to put files in the same output path used by the build scripts and visual studio.

dotnet pack Carambolas\Carambolas.csproj -c Release -p:Platform=x64 --output "%~dp0Build\NuGet"
if %errorlevel% neq 0 goto error 

dotnet pack Carambolas.Net\Carambolas.Net.csproj -c Release -p:Platform=x64 --output "%~dp0Build\NuGet"
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%

