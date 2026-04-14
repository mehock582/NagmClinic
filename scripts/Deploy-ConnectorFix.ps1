param(
    [string]$ServiceName = "NagmLabConnector",
    [string]$StagedPath = "C:\LabConnector\_staged_fix",
    [string]$TargetPath = "C:\LabConnector",
    [int]$ListenerPort = 5000,
    [string]$ClinicUrl = "https://localhost:44356/api/lab-results/import"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Step([string]$Text) {
    Write-Host "==> $Text" -ForegroundColor Cyan
}

if (-not (Test-IsAdmin)) {
    throw "Run this script as Administrator."
}

if (-not (Test-Path $StagedPath)) {
    throw "Staged fix path not found: $StagedPath"
}

Write-Step "Stopping service $ServiceName"
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction Stop
}

Write-Step "Checking legacy outbox location"
$legacyOutboxPath = "C:\Windows\System32\connector-outbox.json"
if (Test-Path $legacyOutboxPath) {
    $backupName = "connector-outbox.system32.backup.{0}.json" -f (Get-Date -Format "yyyyMMddHHmmss")
    $backupPath = Join-Path $TargetPath $backupName
    Copy-Item -Path $legacyOutboxPath -Destination $backupPath -Force
    Write-Host ("Found legacy outbox in System32. Backup saved to {0}" -f $backupPath) -ForegroundColor Yellow
}

Write-Step "Copying fixed files from $StagedPath to $TargetPath"
robocopy $StagedPath $TargetPath /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null

Write-Step "Starting service $ServiceName"
Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
Write-Host ("Service status: {0}" -f $service.Status)

Write-Step "Checking connector listener port $ListenerPort"
$listenerOk = $false
try {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $ListenerPort -ErrorAction Stop | Select-Object -First 1
    if ($listener) {
        $listenerOk = $true
        Write-Host ("Listener active on port {0}, PID {1}" -f $listener.LocalPort, $listener.OwningProcess)
    }
}
catch {
    Write-Host "Listener not detected yet." -ForegroundColor Yellow
}

Write-Step "Checking clinic endpoint reachability"
try {
    $resp = Invoke-WebRequest -Uri $ClinicUrl -Method Get -SkipCertificateCheck -TimeoutSec 10
    Write-Host ("Clinic endpoint responded: HTTP {0}" -f $resp.StatusCode)
}
catch {
    $statusCode = $null
    if ($_.Exception -and $_.Exception.PSObject.Properties.Match("Response").Count -gt 0) {
        $response = $_.Exception.Response
        if ($response -and $response.PSObject.Properties.Match("StatusCode").Count -gt 0) {
            $statusCode = [int]$response.StatusCode
        }
    }

    if ($null -ne $statusCode) {
        Write-Host ("Clinic endpoint reachable: HTTP {0}" -f $statusCode)
    }
    else {
        Write-Host ("Clinic endpoint check failed: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

Write-Host ""
if ($service.Status -eq "Running" -and $listenerOk) {
    Write-Host "Connector fix deployed successfully." -ForegroundColor Green
}
else {
    Write-Host "Deployment finished, but listener is not confirmed yet. Check Application logs for NagmLabConnector." -ForegroundColor Yellow
}
