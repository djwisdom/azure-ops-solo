#!/bin/bash
# Terraform Check Script
set -e

cd infra || exit 1

echo "Running Terraform checks..."

# Format check
terraform fmt -check
echo "Format check passed."

# Validate
terraform init -backend=false
terraform validate
echo "Validation passed."

# Security scan with Checkov
checkov -f . --framework terraform --quiet
echo "Security scan passed."

echo "All Terraform checks completed successfully."