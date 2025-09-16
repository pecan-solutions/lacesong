#!/bin/bash

# Lacesong Development Build Script
# Quick build for development - builds WPF and CLI applications without full packaging

set -e

echo "Building Lacesong for Development..."

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean Lacesong.sln

# Restore packages
echo "Restoring packages..."
dotnet restore Lacesong.sln

# Build Core library first (required dependency)
echo "Building Core library..."
dotnet build src/Lacesong.Core/Lacesong.Core.csproj --configuration Debug --no-restore

# Build CLI application
echo "Building CLI application..."
dotnet build src/Lacesong.CLI/Lacesong.CLI.csproj --configuration Debug --no-restore --runtime win-x64

# Build WPF application (Windows-specific)
echo "Building WPF application..."
dotnet build src/Lacesong.WPF/Lacesong.WPF.csproj --configuration Debug --no-restore --runtime win-x64

# Create dev output directories
echo "Creating development output directories..."
mkdir -p ./dev-build/WPF
mkdir -p ./dev-build/CLI

# Publish WPF application for development
echo "Publishing WPF application for development..."
dotnet publish src/Lacesong.WPF/Lacesong.WPF.csproj \
    --configuration Debug \
    --runtime win-x64 \
    --self-contained false \
    --output ./dev-build/WPF \
    --no-build \
    -p:PublishSingleFile=false

# Publish CLI application for development
echo "Publishing CLI application for development..."
dotnet publish src/Lacesong.CLI/Lacesong.CLI.csproj \
    --configuration Debug \
    --runtime win-x64 \
    --self-contained false \
    --output ./dev-build/CLI \
    --no-build

echo "Development build completed successfully!"
echo ""
echo "Applications ready in ./dev-build/:"
echo "   - WPF: ./dev-build/WPF/Lacesong.WPF.exe"
echo "   - CLI: ./dev-build/CLI/modman.exe"
echo ""
echo "To run the WPF application:"
echo "   ./dev-build/WPF/Lacesong.WPF.exe"
echo ""
echo "To run the CLI application:"
echo "   ./dev-build/CLI/modman.exe --help"
echo ""
echo "Ready for development!"
