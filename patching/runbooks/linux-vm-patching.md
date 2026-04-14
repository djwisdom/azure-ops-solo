# Linux VM Patching Runbook

## Overview
Procedure for applying security patches to Linux VMs in production and non-production.

## Prerequisites
- [ ] Maintenance window approved
- [ ] Wiz scan completed
- [ ] Staging VM patched and validated
- [ ] Snapshot taken

## Procedure

### 1. Pre-Patch
```bash
# Check OS version
cat /etc/os-release

# Check pending updates
sudo apt list --upgradable 2>/dev/null || yum check-update 2>/dev/null
```

### 2. Apply Patches
```bash
# Ubuntu/Debian
sudo apt update && sudo apt upgrade -y
sudo apt autoremove -y

# RHEL/CentOS
sudo yum update -y
```

### 3. Post-Patch
```bash
# Verify kernel version
uname -r

# Check critical services
systemctl is-active sshd
systemctl is-active <app-service>
```

### 4. Log
```powershell
.\patching\scripts\Update-PatchLog.ps1 -Environment "prod" -Platform "linux" `
    -Critical <count> -High <count> -Medium <count> -Status "remediated"
```
