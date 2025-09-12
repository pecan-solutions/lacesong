#!/bin/bash

# Lacesong Build Script
# Builds the entire solution and creates distribution packages

set -e

echo "Building Lacesong..."

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean

# Restore packages
echo "Restoring packages..."
dotnet restore

# Build solution
echo "Building solution..."
dotnet build --configuration Release

# Run tests
echo "Running tests..."
dotnet test --configuration Release --no-build

# Publish CLI tool
echo "Publishing CLI tool..."
dotnet publish src/Lacesong.CLI/Lacesong.CLI.csproj --configuration Release --runtime win-x64 --self-contained true --output build/cli/win-x64
dotnet publish src/Lacesong.CLI/Lacesong.CLI.csproj --configuration Release --runtime osx-x64 --self-contained true --output build/cli/osx-x64
dotnet publish src/Lacesong.CLI/Lacesong.CLI.csproj --configuration Release --runtime linux-x64 --self-contained true --output build/cli/linux-x64

# Create packages
echo "Creating packages..."
mkdir -p build/packages

# Windows package
cd build/cli/win-x64
zip -r ../../packages/lacesong-cli-win-x64.zip .
cd ../../..

# macOS package
cd build/cli/osx-x64
zip -r ../../packages/lacesong-cli-osx-x64.zip .
cd ../../..

# Linux package
cd build/cli/linux-x64
tar -czf ../../packages/lacesong-cli-linux-x64.tar.gz .
cd ../../..

echo "Build completed successfully!"
echo "Packages created in build/packages/"
echo ""
echo "Available packages:"
ls -la build/packages/
