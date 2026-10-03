@echo off
rem Abre o Lumina com o diagnóstico ligado (grava em %APPDATA%\Lumina\diagnostico.txt).
rem Dois cliques no Explorer: roda fora do isolamento do terminal do Claude.
cd /d "%~dp0.."
dotnet run --project src\Lumina -- --diagnostico %*
