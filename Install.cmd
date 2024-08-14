@echo off

:: Determine the directory of this script
set SCRIPT_DIR=%~dp0

:: Define the paths to the services based on the script directory
set MAIN_SERVICE_PATH=%SCRIPT_DIR%/App/StoreVideo-MainService.exe
set VIDEO_SERVICE_PATH=%SCRIPT_DIR%/App/StoreVideo-VideoService.exe

:: Check if the script is running with administrative privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting administrative privileges...
    powershell -command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb runAs"
    exit /b
)

echo Installing StoreVideo-MainService...
"%MAIN_SERVICE_PATH%" install
if %ERRORLEVEL% equ 0 (
    echo StoreVideo-MainService installed successfully.
    net start "StoreVideo-MainService"
    if %ERRORLEVEL% equ 0 (
        echo Started service ==> StoreVideo-MainService

echo Installing StoreVideo-VideoService...
"%VIDEO_SERVICE_PATH%" install
if %ERRORLEVEL% equ 0 (
    echo StoreVideo-VideoService installed successfully.
    net start "StoreVideo-VideoService"
    if %ERRORLEVEL% equ 0 (
        echo Started service ==> StoreVideo-VideoService
    ) else (
        echo Failed to start StoreVideo-VideoService.
    )
) else (
    echo Failed to install StoreVideo-VideoService.
)

    ) else (
        echo Failed to start StoreVideo-MainService.
    )
) else (
    echo Failed to install StoreVideo-MainService.
)



pause
