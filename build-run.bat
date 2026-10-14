@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo 正在关闭现有窗口...
taskkill /F /IM nook.exe 2>nul

dotnet build nook-desktop.slnx --nologo
if errorlevel 1 (
    echo.
    echo 构建失败。
    pause
    exit /b 1
)

start "" "nook-desktop\bin\Debug\net10.0\nook.exe"
