#!/bin/bash
# C# Check Script
set -e

echo "Running C# checks..."

# Format check
dotnet format --check --verbosity quiet apps/MyCrownJewelApp/MyCrownJewelApp.sln
echo "Format check passed."

# Build
dotnet build --configuration Release --no-restore apps/MyCrownJewelApp/MyCrownJewelApp.sln
echo "Build passed."

# Test
dotnet test --configuration Release --no-build --verbosity quiet apps/MyCrownJewelApp/MyCrownJewelApp.sln
echo "Tests passed."

echo "All C# checks completed successfully."