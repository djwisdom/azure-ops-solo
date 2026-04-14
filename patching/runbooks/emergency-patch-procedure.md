# Emergency Patching Procedure

## When to Use
- Zero-day vulnerability actively exploited
- Critical Wiz finding with active exploit (CVE has known exploit)
- Direction from security team or CISO

## Emergency Process

### 1. Assess (5 minutes)
- Wiz scan confirms critical vulnerability
- Exploit is active or imminent
- Affected systems identified

### 2. Communicate (5 minutes)
- Notify security team and management
- Document in patching/compliance/exceptions.md
- Get verbal/written approval to proceed

### 3. Stage (15 minutes)
- Patch one non-production VM first
- Validate application functionality
- If clean, proceed to production

### 4. Patch (30-60 minutes)
- Apply patches to production VMs
- Monitor for failures
- Reboot if required

### 5. Validate (15 minutes)
- Wiz re-scan confirms remediation
- Application health check passes
- Services running

### 6. Document (10 minutes)
```powershell
.\patching\scripts\Update-PatchLog.ps1 `
    -Environment "prod" `
    -Platform "windows" `
    -Critical <count> `
    -Status "remediated" `
    -Note "EMERGENCY: <brief reason>"
```

- Update exception record
- Notify management of completion

## Rollback
- If patch breaks critical service: restore from snapshot
- Document rollback reason
- Re-assess vulnerability risk vs. application availability
