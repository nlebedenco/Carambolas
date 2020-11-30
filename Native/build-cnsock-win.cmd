@echo off
setlocal
pushd %~dp0

set BASEDIR=%~dp0\cnsock
set BUILD_DIR_x86=%BASEDIR%\..\..\Build\Native\cnsock\Release\win-x86\cmake
set BUILD_DIR_x64=%BASEDIR%\..\..\Build\Native\cnsock\Release\win-x64\cmake

mkdir "%BUILD_DIR_x86%"
pushd "%BUILD_DIR_x86%"
cmake -G "Visual Studio 15 2017" "%BASEDIR%" -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="../../win-x86"
if %errorlevel% neq 0 goto error 
popd

cmake --build "%BUILD_DIR_x86%" --config Release --target install 
if %errorlevel% neq 0 goto error 

xcopy "%BUILD_DIR_x86%\..\bin\cnsock.dll" "%~dp0\..\Carambolas.Net\Native\win-x86\" /Y /I /F
if %errorlevel% neq 0 goto error 

echo.

mkdir "%BUILD_DIR_x64%"
pushd "%BUILD_DIR_x64%"
cmake -G "Visual Studio 15 2017 Win64" "%BASEDIR%" -DCMAKE_BUILD_TYPE=Release -DCMAKE_RC_FLAGS="/D_WIN64" -DCMAKE_INSTALL_PREFIX="../../win-x64"
if %errorlevel% neq 0 goto error 
popd

cmake --build "%BUILD_DIR_x64%" --config Release --target install
if %errorlevel% neq 0 goto error 

xcopy "%BUILD_DIR_x64%\..\bin\cnsock.dll" "%~dp0\..\Carambolas.Net\Native\win-x64\" /Y /I /F
if %errorlevel% neq 0 goto error 

:done
echo Done.
goto :EOF

:error
exit /b %errorlevel%
