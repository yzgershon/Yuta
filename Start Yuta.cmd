@echo off
set "YUTA_RUNTIME=win-x64"
if /i "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "YUTA_RUNTIME=win-arm64"
if /i "%PROCESSOR_ARCHITEW6432%"=="ARM64" set "YUTA_RUNTIME=win-arm64"
start "" "%~dp0release\yuta\%YUTA_RUNTIME%\Yuta.exe"
