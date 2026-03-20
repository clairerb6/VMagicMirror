@echo off
REM Example:
REM   .\Batches\update_release_version.cmd 4.5.0 2026/03/31
where pwsh >nul 2>nul
if %errorlevel%==0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0update_release_version.ps1" -Version "%~1" -ReleaseDate "%~2"
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0update_release_version.ps1" -Version "%~1" -ReleaseDate "%~2"
)
