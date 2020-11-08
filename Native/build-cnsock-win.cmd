@echo off
setlocal
pushd %~dp0

set BASEDIR="%~dp0\cnsock"
set BUILD_DIR_x86="%BASEDIR%\..\..\Build\Native\cnsock\Release\cmake\win-x86"
set BUILD_DIR_x64="%BASEDIR%\..\..\Build\Native\cnsock\Release\cmake\win-x64" 

mkdir %BUILD_DIR_X86%
pushd %BUILD_DIR_X86%
cmake -G "Visual Studio 15 2017" %BASEDIR% -DCMAKE_INSTALL_PREFIX="../../bin/win-x86"
if %errorlevel% neq 0 goto error 
popd

cmake --build %BUILD_DIR_X86% --config Release --target INSTALL 
if %errorlevel% neq 0 goto error 

mkdir %BUILD_DIR_X64%
pushd %BUILD_DIR_X64%
cmake -G "Visual Studio 15 2017 Win64" %BASEDIR% -DCMAKE_RC_FLAGS="/D_WIN64" -DCMAKE_INSTALL_PREFIX="../../bin/win-x64"
if %errorlevel% neq 0 goto error 
popd

cmake --build %BUILD_DIR_X64% --config Release --target INSTALL
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%
