#!/bin/bash
# Bicep Check Script
set -e

cd bicep || exit 1

echo "Running Bicep checks..."

# Lint each .bicep file
for file in *.bicep; do
    if [ -f "$file" ]; then
        echo "Linting $file..."
        az bicep lint --file "$file"
    fi
done

echo "All Bicep checks completed successfully."