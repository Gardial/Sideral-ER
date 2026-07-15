@echo off
setlocal
cd /d "%~dp0"
dotnet ".\bin\Release\net9.0-windows\RandomMagicConversion.dll" %*
