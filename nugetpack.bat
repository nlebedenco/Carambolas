@echo off
setlocal
pushd %~dp0

for %%P in (
"Carambolas.Core" 
"Carambolas.Net" 
"Carambolas.Replxx" 
"Carambolas.Runtime.InteropServices" 
) do (
  dotnet pack %%P -c Release -p:Platform=AnyCPU --output "%~dp0Build\NuGet"
  if %errorlevel% neq 0 goto error
)

:done
echo Done.
goto :EOF

rem Error happened. Wait for a keypress before quitting.
:error
exit /b %errorlevel%

