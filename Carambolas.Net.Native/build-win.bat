@echo off
setlocal
pushd %~dp0

mkdir "%~dp0..\Build\CMake\win-x86"
pushd "%~dp0..\Build\CMake\win-x86"
cmake -G "Visual Studio 15 2017" %~dp0 -DCMAKE_INSTALL_PREFIX="../../Native/runtimes/win-x86"
if %errorlevel% neq 0 goto error 
popd

mkdir "%~dp0..\Build\CMake\win-x64" 
pushd "%~dp0..\Build\CMake\win-x64"
cmake -G "Visual Studio 15 2017 Win64" %~dp0 -DCMAKE_RC_FLAGS="/D_WIN64" -DCMAKE_INSTALL_PREFIX="../../Native/runtimes/win-x64"
if %errorlevel% neq 0 goto error 
popd

cmake --build "%~dp0..\Build\CMake\win-x86" --config Release --target INSTALL 
if %errorlevel% neq 0 goto error 

cmake --build "%~dp0..\Build\CMake\win-x64" --config Release --target INSTALL
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%
