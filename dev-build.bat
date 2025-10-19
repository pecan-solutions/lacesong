@echo off
REM Lacesong Development Build Script
REM Quick build for development - builds WPF and CLI applications without full packaging

echo Building Lacesong for Development...

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean Lacesong.sln

REM Restore packages
echo Restoring packages...
dotnet restore Lacesong.sln

REM Build Core library first (required dependency)
echo Building Core library...
dotnet build src/Lacesong.Core/Lacesong.Core.csproj --configuration Debug --no-restore


REM Build WPF application
echo Building WPF application...
dotnet build src/Lacesong.WPF/Lacesong.WPF.csproj --configuration Debug --no-restore --runtime win-x64

REM Create dev output directories
echo Creating development output directories...
if not exist ".\dev-build" mkdir ".\dev-build"
if not exist ".\dev-build\WPF" mkdir ".\dev-build\WPF"

REM Publish WPF application for development
echo Publishing WPF application for development...
dotnet publish src/Lacesong.WPF/Lacesong.WPF.csproj ^
    --configuration Debug ^
    --runtime win-x64 ^
    --self-contained false ^
    --output ".\dev-build\WPF" ^
    --no-build ^
    -p:PublishSingleFile=false


echo Development build completed successfully!
echo.
echo Applications ready in ./dev-build/:
echo    - WPF: ./dev-build/WPF/Lacesong.WPF.exe
echo.
echo To run the WPF application:
echo    ./dev-build/WPF/Lacesong.WPF.exe
echo.
echo Ready for development!
