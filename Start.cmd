@echo off
if not exist "%~dp0out\CodexUsageOverlay.exe" (
    echo Build the project first, or extract the complete portable release ZIP.
    pause
    exit /b 1
)
start "" /d "%~dp0" "%~dp0out\CodexUsageOverlay.exe"

