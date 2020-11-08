@echo off

reg Query "HKLM\Hardware\Description\System\CentralProcessor\0" | find /i "x86" > NUL && set OS=x86 || set OS=x64

dotnet %~dp0..\Build\Carambolas.Net.Tests.Application\bin\%OS%\Debug\Carambolas.Net.Tests.Application.dll %*