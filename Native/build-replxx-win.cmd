@echo off
setlocal
pushd %~dp0

set BASEDIR="%~dp0\replxx"
set BUILD_DIR_x86="%BASEDIR%\..\..\Build\Native\replxx\Release\cmake\win-x86"
set BUILD_DIR_x64="%BASEDIR%\..\..\Build\Native\replxx\Release\cmake\win-x64" 

mkdir %BUILD_DIR_X86%
pushd %BUILD_DIR_X86%
cmake -G "Visual Studio 15 2017" %BASEDIR% -DCMAKE_INSTALL_PREFIX="../../bin/win-x86" -DBUILD_SHARED_LIBS=ON -DREPLXX_BUILD_EXAMPLES=OFF
if %errorlevel% neq 0 goto error 
popd

cmake --build %BUILD_DIR_X86% --config release --target install 
if %errorlevel% neq 0 goto error 

mkdir %BUILD_DIR_X64%
pushd %BUILD_DIR_X64%
cmake -G "Visual Studio 15 2017 Win64" %BASEDIR% -DCMAKE_RC_FLAGS="/D_WIN64" -DCMAKE_INSTALL_PREFIX="../../bin/win-x64" -DBUILD_SHARED_LIBS=ON -DREPLXX_BUILD_EXAMPLES=OFF
if %errorlevel% neq 0 goto error 
popd

cmake --build %BUILD_DIR_X64% --config release --target install
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%
