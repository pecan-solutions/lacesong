@echo off
REM Lacesong Build Script
REM Builds all projects and creates distribution packages

echo Building Lacesong Mod Manager...

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean Lacesong.sln

REM Restore packages
echo Restoring packages...
dotnet restore Lacesong.sln

REM Build all projects
echo Building all projects...
dotnet build Lacesong.sln --configuration Release --no-restore

REM Run tests
echo Running tests...
dotnet test Lacesong.sln --configuration Release --no-build --verbosity normal

REM Publish WPF application
echo Publishing WPF application...
dotnet publish src/Lacesong.WPF/Lacesong.WPF.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output ./dist/Lacesong.WPF ^
    --no-build


REM Create distribution packages
echo Creating distribution packages...

REM Create WPF installer directory
if not exist ./dist/installer mkdir ./dist/installer
xcopy /E /I ./dist/Lacesong.WPF ./dist/installer


REM Create zip packages
echo Creating zip packages...
cd ./dist

REM WPF Application Package
powershell -ExecutionPolicy Bypass -Command "Compress-Archive -Path installer -DestinationPath Lacesong-WPF-%date:~-4,4%%date:~-10,2%%date:~-7,2%.zip"


cd ..

echo Build completed successfully!
echo.
echo Distribution files created in ./dist/:
echo    - Lacesong-WPF-%date:~-4,4%%date:~-10,2%%date:~-7,2%.zip (WPF Application)
echo.
echo Ready for distribution!
