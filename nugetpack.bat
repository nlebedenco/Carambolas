@echo off
setlocal
pushd %~dp0

call %~dp0Carambolas.Net.Native\build-win.bat
if %errorlevel% neq 0 goto error 

dotnet pack Carambolas.Net.Native\nuget\Carambolas.Net.Native.Win.csproj -c Release --output "%~dp0Build\NuGet"
if %errorlevel% neq 0 goto error 

dotnet pack Carambolas\Carambolas.csproj -c Release --output "%~dp0Build\NuGet"
if %errorlevel% neq 0 goto error 

dotnet pack Carambolas.Net\Carambolas.Net.csproj -c Release --output "%~dp0Build\NuGet"
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%

