@echo off
echo [PureUpdate] Build portable (single exe)...
taskkill /F /IM PureUpdate.exe 2>nul
if exist "publish\" rd /s /q "publish\"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)
echo [OK] Executable: publish\PureUpdate.exe
pause
