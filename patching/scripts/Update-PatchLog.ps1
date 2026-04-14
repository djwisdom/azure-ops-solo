<#
.SYNOPSIS
    Updates the patch compliance log with a new session entry.
.DESCRIPTION
    Appends a single patch session record to the monthly summary.
    Takes counts only — no hostnames, IPs, or CVE details.
    This is the ONE COMMAND you run after each patch session.
.EXAMPLE
    .\Update-PatchLog.ps1 -Environment "prod" -Platform "windows" -Critical 3 -High 9 -Status "remediated"
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("prod", "nonprod", "staging")]
    [string]$Environment,

    [Parameter(Mandatory = $true)]
    [ValidateSet("windows", "linux", "aks")]
    [string]$Platform,

    [Parameter(Mandatory = $false)]
    [int]$Critical = 0,

    [Parameter(Mandatory = $false)]
    [int]$High = 0,

    [Parameter(Mandatory = $false)]
    [int]$Medium = 0,

    [Parameter(Mandatory = $false)]
    [int]$Low = 0,

    [Parameter(Mandatory = $true)]
    [ValidateSet("remediated", "partial", "deferred", "failed")]
    [string]$Status,

    [Parameter(Mandatory = $false)]
    [string]$Note = "",

    [Parameter(Mandatory = $false)]
    [string]$SummaryPath = "patching/compliance/monthly-summary.json"
)

$session = @{
    date        = (Get-Date -Format "yyyy-MM-dd")
    time        = (Get-Date -Format "HH:mm")
    environment = $Environment
    platform    = $Platform
    critical    = $Critical
    high        = $High
    medium      = $Medium
    low         = $Low
    status      = $Status
    note        = $Note
}

# Load existing summary or create new
$metrics = $null
if (Test-Path $SummaryPath) {
    $metrics = Get-Content $SummaryPath | ConvertFrom-Json
} else {
    $metrics = @{
        date            = (Get-Date -Format "yyyy-MM")
        patch_sessions  = @()
    }
}

# Ensure patch_sessions is an array
if (-not $metrics.patch_sessions) {
    $metrics.patch_sessions = @()
}

# Add new session
$metrics.patch_sessions += $session

# Save
$dir = Split-Path $SummaryPath -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

$metrics | ConvertTo-Json -Depth 5 | Set-Content $SummaryPath -Encoding UTF8

Write-Host "Patch session recorded:"
Write-Host "  Env: $Environment | Platform: $Platform"
Write-Host "  Critical: $Critical | High: $High | Medium: $Medium | Low: $Low"
Write-Host "  Status: $Status"
if ($Note) { Write-Host "  Note: $Note" }
Write-Host ""
Write-Host "Run 'git add $SummaryPath && git commit -m \"Patch session: $Environment $Platform - $Status\"'"
