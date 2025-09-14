@echo off
REM Lacesong Build Script for Windows
REM Builds all projects and creates distribution packages

echo 🎮 Building Lacesong Mod Manager...

REM Clean previous builds
echo 🧹 Cleaning previous builds...
dotnet clean Lacesong.sln

REM Restore packages
echo 📦 Restoring packages...
dotnet restore Lacesong.sln

REM Build all projects
echo 🔨 Building all projects...
dotnet build Lacesong.sln --configuration Release --no-restore

REM Run tests
echo 🧪 Running tests...
dotnet test Lacesong.sln --configuration Release --no-build --verbosity normal

REM Publish WPF application
echo 📱 Publishing WPF application...
dotnet publish src\Lacesong.WPF\Lacesong.WPF.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output .\dist\Lacesong.WPF ^
    --no-build

REM Publish CLI application
echo 💻 Publishing CLI application...
dotnet publish src\Lacesong.CLI\Lacesong.CLI.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output .\dist\Lacesong.CLI ^
    --no-build

REM Create distribution packages
echo 📦 Creating distribution packages...

REM Create WPF installer directory
if not exist .\dist\installer mkdir .\dist\installer
xcopy .\dist\Lacesong.WPF\* .\dist\installer\ /E /I /Y

REM Create CLI package
if not exist .\dist\cli-package mkdir .\dist\cli-package
xcopy .\dist\Lacesong.CLI\* .\dist\cli-package\ /E /I /Y
copy README.md .\dist\cli-package\
copy USAGE.md .\dist\cli-package\
copy LICENSE .\dist\cli-package\

echo ✅ Build completed successfully!
echo.
echo 📁 Distribution files created in .\dist\:
echo    - installer\ (WPF Application)
echo    - cli-package\ (CLI Package)
echo.
echo 🚀 Ready for distribution!

pause
