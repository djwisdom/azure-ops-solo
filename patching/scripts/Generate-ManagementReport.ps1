<#
.SYNOPSIS
    Generates a management-ready patch compliance report from the monthly summary.
.DESCRIPTION
    Reads patching/compliance/monthly-summary.json and produces a markdown report
    suitable for management review. No hostnames, IPs, or CVE details.
.OUTPUTS
    Markdown report to patching/reports/management-report-YYYY-MM.md
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$SummaryPath = "patching/compliance/monthly-summary.json",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "patching/reports"
)

if (-not (Test-Path $SummaryPath)) {
    Write-Error "No summary found at $SummaryPath. Run Get-PatchMetrics.ps1 first."
    exit 1
}

$metrics = Get-Content $SummaryPath | ConvertFrom-Json
$month = $metrics.date
$sessions = $metrics.patch_sessions

# Calculate totals
$totalCritical   = ($sessions | Measure-Object -Property critical -Sum).Sum
$totalRemediated = $sessions | Where-Object { $_.status -eq "remediated" } | Measure-Object -Property critical -Sum
$totalHigh       = ($sessions | Measure-Object -Property high -Sum).Sum
$totalMedium     = ($sessions | Measure-Object -Property medium -Sum).Sum
$totalLow        = ($sessions | Measure-Object -Property low -Sum).Sum
$complianceRate  = if ($totalCritical -gt 0) { [math]::Round(($totalRemediated.Sum / $totalCritical) * 100, 1) } else { 100 }

$report = @"
# Patch Compliance Report — $month

## Executive Summary
| Severity | Total Found | Remediated | Pending | Compliance Rate |
|----------|-------------|------------|---------|-----------------|
| Critical | $totalCritical | $($totalRemediated.Sum) | $($totalCritical - $totalRemediated.Sum) | ${complianceRate}% |
| High     | $totalHigh | TBD | TBD | TBD |
| Medium   | $totalMedium | TBD | TBD | TBD |
| Low      | $totalLow | TBD | TBD | TBD |

## Patch Sessions This Month
| Date | Environment | Platform | Critical | High | Medium | Status | Notes |
|------|-------------|----------|----------|------|--------|--------|-------|
"@

foreach ($s in $sessions) {
    $report += "`n| $($s.date) | $($s.environment) | $($s.platform) | $($s.critical) | $($s.high) | $($s.medium) | $($s.status) | $($s.note) |"
}

$report += @"


## Outstanding Items
$(@($sessions | Where-Object { $_.status -ne "remediated" }).Count) session(s) not fully remediated.
See individual session records for details.

---
*Generated on $(Get-Date -Format "yyyy-MM-dd HH:mm"). No hostnames, IPs, or CVE details included.*
"@

# Save
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null }
$reportFile = Join-Path $OutputDir "management-report-$month.md"
$report | Set-Content $reportFile -Encoding UTF8

Write-Host "Management report generated: $reportFile"
