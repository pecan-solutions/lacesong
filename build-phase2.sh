#!/bin/bash

# Lacesong Build Script
# Builds all projects and creates distribution packages

set -e

echo "🎮 Building Lacesong Mod Manager..."

# Clean previous builds
echo "🧹 Cleaning previous builds..."
dotnet clean Lacesong.sln

# Restore packages
echo "📦 Restoring packages..."
dotnet restore Lacesong.sln

# Build all projects
echo "🔨 Building all projects..."
dotnet build Lacesong.sln --configuration Release --no-restore

# Run tests
echo "🧪 Running tests..."
dotnet test Lacesong.sln --configuration Release --no-build --verbosity normal

# Publish WPF application
echo "📱 Publishing WPF application..."
dotnet publish src/Lacesong.WPF/Lacesong.WPF.csproj \
    --configuration Release \
    --runtime win-x64 \
    --self-contained true \
    --output ./dist/Lacesong.WPF \
    --no-build

# Publish CLI application
echo "💻 Publishing CLI application..."
dotnet publish src/Lacesong.CLI/Lacesong.CLI.csproj \
    --configuration Release \
    --runtime win-x64 \
    --self-contained true \
    --output ./dist/Lacesong.CLI \
    --no-build

# Create distribution packages
echo "📦 Creating distribution packages..."

# Create WPF installer directory
mkdir -p ./dist/installer
cp -r ./dist/Lacesong.WPF/* ./dist/installer/

# Create CLI package
mkdir -p ./dist/cli-package
cp -r ./dist/Lacesong.CLI/* ./dist/cli-package/
cp README.md ./dist/cli-package/
cp USAGE.md ./dist/cli-package/
cp LICENSE ./dist/cli-package/

# Create zip packages
echo "🗜️ Creating zip packages..."
cd ./dist

# WPF Application Package
zip -r Lacesong-WPF-$(date +%Y%m%d).zip installer/

# CLI Package
zip -r Lacesong-CLI-$(date +%Y%m%d).zip cli-package/

cd ..

echo "✅ Build completed successfully!"
echo ""
echo "📁 Distribution files created in ./dist/:"
echo "   - Lacesong-WPF-$(date +%Y%m%d).zip (WPF Application)"
echo "   - Lacesong-CLI-$(date +%Y%m%d).zip (CLI Package)"
echo ""
echo "🚀 Ready for distribution!"
