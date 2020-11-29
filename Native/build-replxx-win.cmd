@echo off
setlocal
pushd %~dp0

set BASEDIR=%~dp0\replxx
set BUILD_DIR_x86=%BASEDIR%\..\..\Build\Native\replxx\Release\win-x86\cmake
set BUILD_DIR_x64=%BASEDIR%\..\..\Build\Native\replxx\Release\win-x64\cmake

mkdir "%BUILD_DIR_x86%"
pushd "%BUILD_DIR_x86%"
cmake -G "Visual Studio 15 2017" "%BASEDIR%" -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="../../win-x86" -DBUILD_SHARED_LIBS=ON -DREPLXX_BUILD_EXAMPLES=OFF
if %errorlevel% neq 0 goto error 
popd

cmake --build "%BUILD_DIR_x86%" --config Release --target install
if %errorlevel% neq 0 goto error 
 
xcopy "%BUILD_DIR_x86%\..\bin\replxx.dll" "%~dp0\..\Carambolas.UI.Replxx\Native\win-x86\" /Y /I /F
if %errorlevel% neq 0 goto error 

echo.

mkdir %BUILD_DIR_x64%
pushd %BUILD_DIR_x64%
cmake -G "Visual Studio 15 2017 Win64" "%BASEDIR%" -DCMAKE_BUILD_TYPE=Release -DCMAKE_RC_FLAGS="/D_WIN64" -DCMAKE_INSTALL_PREFIX="../../win-x64" -DBUILD_SHARED_LIBS=ON -DREPLXX_BUILD_EXAMPLES=OFF
if %errorlevel% neq 0 goto error 
popd

cmake --build "%BUILD_DIR_x64%" --config Release --target install
if %errorlevel% neq 0 goto error 

xcopy "%BUILD_DIR_x64%\..\bin\replxx.dll" "%~dp0\..\Carambolas.UI.Replxx\Native\win-x64\" /Y /I /F
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%
