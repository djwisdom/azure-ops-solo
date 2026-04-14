# Windows VM Patching Runbook

## Overview
Procedure for applying security patches to Windows Server VMs in production and
non-production environments.

## Prerequisites
- [ ] Maintenance window approved
- [ ] Wiz scan completed and critical findings documented
- [ ] Staging VM patched first and validated for 24 hours
- [ ] Backup/snapshot of target VMs taken
- [ ] Rollback plan documented

## Procedure

### 1. Pre-Patch Checks
```powershell
# Check VM accessibility
Test-Connection -ComputerName <VM_HOSTNAME> -Count 2

# Check pending updates
Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 5

# Check disk space (patches need ~5GB free)
Get-PSDrive C | Select-Object Used, Free
```

### 2. Apply Patches
```powershell
# Option A: WSUS-managed
Install-WindowsUpdate -AcceptAll -AutoReboot

# Option B: Manual KB installation
wusa.exe <KB_NUMBER>.msu /quiet /norestart
```

### 3. Post-Patch Validation
```powershell
# Verify patches installed
Get-HotFix -Id KB<NUMBER>

# Check service health
Get-Service | Where-Object { $_.StartType -eq 'Automatic' -and $_.Status -ne 'Running' }

# Reboot if required
Restart-Computer -Force
```

### 4. Compliance Logging
```powershell
# Update patch log (sanitized — no hostnames or IPs)
.\patching\scripts\Update-PatchLog.ps1 -Environment "prod" -Platform "windows" `
    -Critical <count> -High <count> -Medium <count> -Status "remediated"
```

## Rollback
```powershell
# Uninstall specific KB
wusa.exe /uninstall /kb:<KB_NUMBER> /quiet /norestart

# Restore from snapshot (Azure)
# See: rollback-snapshot.md
```

## Post-Patch
- [ ] Wiz re-scan confirms remediation
- [ ] Compliance log updated
- [ ] Management report updated (counts only)
- [ ] Next maintenance window scheduled if deferred
