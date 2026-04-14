<#
GENERAL LAB CONNECTOR TEST SCRIPT
---------------------------------
Use this script to send test results from PowerShell through the connector listener
without asking for manual payload creation each time.

WHAT YOU MUST PREPARE (YOUR SIDE)
1) Clinic app is running (example: https://localhost:44356).
2) Connector Windows service is running (default: NagmLabConnector).
3) The visit exists in Clinic and has requested lab tests.
4) Test codes you send must match requested test codes in that visit
   (or have Device->Test mapping configured).
5) SampleId must map to visit number format:
   LAB-YYYYMMDD-VisitNumber
   Example: LAB-20260411-26041102

QUICK EXAMPLES
1) Abnormal values for PSA and HFABP in visit 26041102:
   powershell -ExecutionPolicy Bypass -File .\scripts\Run-LabConnectorTest.ps1 -VisitNumber 26041102 -TestCodes "PSA,HFABP" -Mode Abnormal

2) Normal values for MCV and LYM#:
   powershell -ExecutionPolicy Bypass -File .\scripts\Run-LabConnectorTest.ps1 -VisitNumber 26041101 -TestCodes "MCV,LYM#" -Mode Normal

3) Custom exact values:
   powershell -ExecutionPolicy Bypass -File .\scripts\Run-LabConnectorTest.ps1 -VisitNumber 26041102 -Mode Custom -CustomTests "PSA=8.9,HFABP=12.4"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$VisitNumber,

    [ValidateSet("Normal", "Abnormal", "Custom")]
    [string]$Mode = "Abnormal",

    [string]$TestCodes = "PSA,HFABP",

    [string]$CustomTests = "",

    [string]$SampleId = "",

    [string]$DatePart = "",

    [string]$TargetHost = "127.0.0.1",
    [int]$Port = 5000,
    [ValidateSet("HL7", "TEXT", "CSV", "PIPE", "RAW")]
    [string]$Protocol = "HL7",

    [switch]$RunDebugCheck,
    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-TestValue {
    param(
        [string]$Code,
        [string]$ValueMode
    )

    $normalized = $Code.Trim().ToUpperInvariant()
    $table = @{
        "PSA"   = @{ Normal = "2.1";  Abnormal = "8.9"  }
        "HFABP" = @{ Normal = "3.0";  Abnormal = "12.4" }
        "MCV"   = @{ Normal = "90.0"; Abnormal = "108.0" }
        "LYM#"  = @{ Normal = "2.0";  Abnormal = "5.8"  }
        "WBC"   = @{ Normal = "7.0";  Abnormal = "16.5" }
        "HGB"   = @{ Normal = "14.0"; Abnormal = "7.8"  }
        "HCT"   = @{ Normal = "42.0"; Abnormal = "58.0" }
        "PRL"   = @{ Normal = "10.0"; Abnormal = "35.0" }
    }

    if ($table.ContainsKey($normalized)) {
        return $table[$normalized][$ValueMode]
    }

    if ($ValueMode -eq "Normal") {
        return "5.0"
    }

    return "15.0"
}

function Build-TestsFromCodes {
    param(
        [string]$CodesCsv,
        [string]$ValueMode
    )

    $parts = $CodesCsv.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -eq 0) {
        throw "No test codes provided."
    }

    $pairs = @()
    foreach ($part in $parts) {
        $code = $part.Trim().ToUpperInvariant()
        if ([string]::IsNullOrWhiteSpace($code)) {
            continue
        }

        $value = Get-TestValue -Code $code -ValueMode $ValueMode
        $pairs += "$code=$value"
    }

    if ($pairs.Count -eq 0) {
        throw "No valid test codes after parsing."
    }

    return ($pairs -join ",")
}

$resolvedDate = if ([string]::IsNullOrWhiteSpace($DatePart)) {
    (Get-Date).ToString("yyyyMMdd")
}
else {
    $DatePart.Trim()
}

$resolvedSampleId = if ([string]::IsNullOrWhiteSpace($SampleId)) {
    "LAB-$resolvedDate-$VisitNumber"
}
else {
    $SampleId.Trim()
}

$testsToSend = switch ($Mode) {
    "Custom" {
        if ([string]::IsNullOrWhiteSpace($CustomTests)) {
            throw "Mode=Custom requires -CustomTests like 'PSA=8.9,HFABP=12.4'."
        }
        $CustomTests.Trim()
    }
    "Normal" {
        Build-TestsFromCodes -CodesCsv $TestCodes -ValueMode "Normal"
    }
    default {
        Build-TestsFromCodes -CodesCsv $TestCodes -ValueMode "Abnormal"
    }
}

$sendScript = Join-Path $PSScriptRoot "Send-ConnectorTestData.ps1"
if (-not (Test-Path $sendScript)) {
    throw "Required script not found: $sendScript"
}

Write-Host ""
Write-Host "Preparing connector test payload..." -ForegroundColor Cyan
Write-Host ("VisitNumber : {0}" -f $VisitNumber)
Write-Host ("SampleId    : {0}" -f $resolvedSampleId)
Write-Host ("Mode        : {0}" -f $Mode)
Write-Host ("Tests       : {0}" -f $testsToSend)
Write-Host ("Target      : {0}:{1}" -f $TargetHost, $Port)

& powershell -ExecutionPolicy Bypass -File $sendScript `
    -TargetHost $TargetHost `
    -Port $Port `
    -Protocol $Protocol `
    -SampleId $resolvedSampleId `
    -Tests $testsToSend

if ($RunDebugCheck) {
    $debugScript = Join-Path $PSScriptRoot "Debug-ConnectorFlow.ps1"
    if (Test-Path $debugScript) {
        $firstCode = ($testsToSend.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)[0].Split("=")[0]).Trim()
        $firstValue = ($testsToSend.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)[0].Split("=")[1]).Trim()

        Write-Host ""
        Write-Host "Running debug check..." -ForegroundColor Cyan
        & powershell -ExecutionPolicy Bypass -File $debugScript `
            -SampleId $resolvedSampleId `
            -TestCode $firstCode `
            -ResultValue $firstValue `
            -SkipCertificateCheck:$SkipCertificateCheck
    }
}

Write-Host ""
Write-Host "Done. Refresh the Laboratory page to verify results." -ForegroundColor Green
