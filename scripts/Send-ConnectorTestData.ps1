param(
    [Alias("Host")]
    [string]$TargetHost = "127.0.0.1",
    [int]$Port = 5000,
    [ValidateSet("HL7", "TEXT", "CSV", "PIPE", "RAW")]
    [string]$Protocol = "HL7",
    [string]$SampleId = "LAB-20260411-1",
    [string]$DeviceId = "SIM-PS",
    [string]$Tests = "WBC=6.2,HGB=13.5,HCT=42.5",
    [string]$Raw = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-TestPairs([string]$value) {
    $pairs = @()
    if ([string]::IsNullOrWhiteSpace($value)) { return $pairs }

    foreach ($segment in $value.Split(',', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $kv = $segment.Split('=', 2)
        if ($kv.Length -ne 2) { continue }
        $pairs += [PSCustomObject]@{
            Code = $kv[0].Trim()
            Value = $kv[1].Trim()
        }
    }
    return $pairs
}

function Build-Frame {
    param(
        [string]$Mode,
        [string]$Sample,
        [string]$Device,
        [object[]]$Pairs,
        [string]$RawInput
    )

    if ($Mode -eq "RAW") {
        if ([string]::IsNullOrWhiteSpace($RawInput)) {
            throw "RAW mode requires -Raw payload."
        }
        return $RawInput
    }

    if ($Pairs.Count -eq 0) {
        throw "No test pairs found. Use -Tests like 'WBC=6.2,HGB=13.5'."
    }

    switch ($Mode) {
        "HL7" {
            $lines = @()
            $now = (Get-Date).ToString("yyyyMMddHHmmss")
            $lines += "MSH|^~\&|PS-SIM|LAB|NAGM|CLINIC|$now||ORU^R01|1|P|2.3"
            $lines += "OBR|1||$Sample"
            $idx = 1
            foreach ($pair in $Pairs) {
                $lines += "OBX|$idx|NM|$($pair.Code)||$($pair.Value)|||||F"
                $idx++
            }
            return ($lines -join "`r")
        }
        "TEXT" {
            $lines = @("ID: $Sample", "DEVICE: $Device")
            foreach ($pair in $Pairs) {
                $lines += "$($pair.Code):$($pair.Value)"
            }
            return ($lines -join "`n")
        }
        "CSV" {
            $lines = @()
            foreach ($pair in $Pairs) {
                $lines += "$Sample,$($pair.Code),$($pair.Value),,$(Get-Date -Format s)"
            }
            return ($lines -join "`n")
        }
        "PIPE" {
            $lines = @()
            foreach ($pair in $Pairs) {
                $lines += "$Sample|$($pair.Code)|$($pair.Value)||$(Get-Date -Format s)"
            }
            return ($lines -join "`n")
        }
        default {
            throw "Unsupported protocol mode: $Mode"
        }
    }
}

$pairs = Parse-TestPairs -value $Tests
$payload = Build-Frame -Mode $Protocol -Sample $SampleId -Device $DeviceId -Pairs $pairs -RawInput $Raw

Write-Host "Sending $Protocol payload to connector listener $TargetHost`:$Port ..."
$client = [System.Net.Sockets.TcpClient]::new()
try {
    $client.Connect($TargetHost, $Port)
    $stream = $client.GetStream()
    $writer = [System.IO.StreamWriter]::new($stream)
    $writer.NewLine = "`r"
    $writer.Write($payload)
    $writer.Write($writer.NewLine)
    $writer.Flush()
    $writer.Dispose()
    $stream.Dispose()
    Write-Host "Payload sent successfully."
}
finally {
    $client.Dispose()
}

Write-Host ""
Write-Host "Payload preview:"
Write-Host "----------------"
Write-Host $payload
