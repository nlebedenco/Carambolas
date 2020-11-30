@echo off
setlocal

if "%1" EQU "" (
echo Create symbolic links to the unity package folders.
echo Usage: %~f0 ^<TARGET DIR^>
goto :error
)

set TARGET=%1

for %%P in (
"dev.nlebedenco.carambolas.core" 
"dev.nlebedenco.carambolas.net" 
"dev.nlebedenco.carambolas.runtime.interopservices" 
"dev.nlebedenco.carambolas.ui.replxx" 
"dotnet.system.memory" 
) do (
  mklink /D %TARGET%\%%P "%~dp0Build\Unity\Debug\%%P"
  if %errorlevel% neq 0 goto error
)

for %%P in (
"dev.nlebedenco.carambolas.unity"
"dev.nlebedenco.carambolas.unity.cli"
"dev.nlebedenco.carambolas.unity.replication"
) do (
  mklink /D %TARGET%\%%P "%~dp0\Unity\%%P"
  if %errorlevel% neq 0 goto error
)

:done
echo Done.
goto :EOF

:error
exit /b %errorlevel%
