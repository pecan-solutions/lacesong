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


# Build WPF application (Windows-specific)
echo "Building WPF application..."
dotnet build src/Lacesong.WPF/Lacesong.WPF.csproj --configuration Debug --no-restore --runtime win-x64

# Build Avalonia application
echo "Building Avalonia application..."
dotnet build src/Lacesong.Avalonia/Lacesong.Avalonia.csproj --configuration Debug --no-restore

# Create dev output directories
echo "Creating development output directories..."
mkdir -p ./dev-build/WPF
mkdir -p ./dev-build/Avalonia

# Publish WPF application for development
echo "Publishing WPF application for development..."
dotnet publish src/Lacesong.WPF/Lacesong.WPF.csproj \
    --configuration Debug \
    --runtime win-x64 \
    --self-contained false \
    --output ./dev-build/WPF \
    --no-build \
    -p:PublishSingleFile=false


# Publish Avalonia application for development
echo "Publishing Avalonia application for development..."
dotnet publish src/Lacesong.Avalonia/Lacesong.Avalonia.csproj \
    --configuration Debug \
    --self-contained false \
    --output ./dev-build/Avalonia \
    --no-build

echo "Development build completed successfully!"
echo ""
echo "Applications ready in ./dev-build/:"
echo "   - WPF: ./dev-build/WPF/Lacesong.WPF.exe"
echo "   - Avalonia: ./dev-build/Avalonia/Lacesong.Avalonia"
echo ""
echo "To run the WPF application:"
echo "   ./dev-build/WPF/Lacesong.WPF.exe"
echo ""
echo "To run the Avalonia application:"
echo "   ./dev-build/Avalonia/Lacesong.Avalonia"
echo ""
echo "Ready for development!"
