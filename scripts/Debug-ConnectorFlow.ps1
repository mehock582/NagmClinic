param(
    [string]$ConnectorServiceName = "NagmLabConnector",
    [string]$ConnectorConfigPath = "C:\LabConnector\appsettings.json",
    [string]$ConnectorHost = "127.0.0.1",
    [int]$ConnectorPort = 5000,
    [string]$SampleId = "LAB-20260411-26041101",
    [string]$TestCode = "MCV",
    [string]$ResultValue = "89.4",
    [string]$Unit = "fL",
    [switch]$SendTest,
    [int]$WaitSeconds = 8,
    [switch]$DirectApiCheck,
    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($SkipCertificateCheck) {
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    }
    catch {
        # Ignore legacy runtime failures; modern cmdlets may still support -SkipCertificateCheck.
    }
}

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Resolve-OutboxPath([string]$QueueFilePath, [string]$ConfigDirectory) {
    if ([string]::IsNullOrWhiteSpace($QueueFilePath)) {
        throw "ConnectorDispatch.QueueFilePath is missing."
    }

    if ([System.IO.Path]::IsPathRooted($QueueFilePath)) {
        return $QueueFilePath
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ConfigDirectory $QueueFilePath))
}

function Test-TcpEndpoint([string]$HostName, [int]$PortNumber, [int]$TimeoutMs = 3000) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync($HostName, $PortNumber)
        if (-not $task.Wait($TimeoutMs)) {
            return $false
        }
        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Send-Hl7Payload {
    param(
        [string]$HostName,
        [int]$PortNumber,
        [string]$PatientSampleId,
        [string]$Code,
        [string]$Value
    )

    $now = Get-Date -Format "yyyyMMddHHmmss"
    $payload = "MSH|^~\&|PS-DEBUG|LAB|NAGM|CLINIC|$now||ORU^R01|1|P|2.3`rOBR|1||$PatientSampleId`rOBX|1|NM|$Code||$Value|||||F`r"

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $client.Connect($HostName, $PortNumber)
        $stream = $client.GetStream()
        $writer = [System.IO.StreamWriter]::new($stream)
        $writer.NewLine = "`r"
        $writer.Write($payload)
        $writer.Write($writer.NewLine)
        $writer.Flush()
        $writer.Dispose()
        $stream.Dispose()
    }
    finally {
        $client.Dispose()
    }

    return $payload
}

function Invoke-WebRequestCompat {
    param(
        [hashtable]$Params,
        [switch]$UseSkipCertificateCheck
    )

    if ($UseSkipCertificateCheck -and (Get-Command Invoke-WebRequest).Parameters.ContainsKey("SkipCertificateCheck")) {
        $Params["SkipCertificateCheck"] = $true
    }

    return Invoke-WebRequest @Params
}

function Invoke-RestMethodCompat {
    param(
        [hashtable]$Params,
        [switch]$UseSkipCertificateCheck
    )

    if ($UseSkipCertificateCheck -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $Params["SkipCertificateCheck"] = $true
    }

    return Invoke-RestMethod @Params
}

Write-Section "Service Info"
$service = $null
try {
    $service = Get-CimInstance Win32_Service -Filter "Name='$ConnectorServiceName'"
    if ($null -eq $service) {
        Write-Host "Service not found: $ConnectorServiceName" -ForegroundColor Yellow
    }
    else {
        Write-Host ("Name      : {0}" -f $service.Name)
        Write-Host ("State     : {0}" -f $service.State)
        Write-Host ("Path      : {0}" -f $service.PathName)
        Write-Host ("ProcessId : {0}" -f $service.ProcessId)
        Write-Host ("StartName : {0}" -f $service.StartName)
    }
}
catch {
    Write-Host ("Could not read service details: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
}

Write-Section "Config Info"
if (-not (Test-Path $ConnectorConfigPath)) {
    throw "Connector config not found: $ConnectorConfigPath"
}

$config = Get-Content $ConnectorConfigPath -Raw | ConvertFrom-Json
$configDir = Split-Path $ConnectorConfigPath -Parent
$queuePath = Resolve-OutboxPath -QueueFilePath $config.ConnectorDispatch.QueueFilePath -ConfigDirectory $configDir
$importUrl = "{0}/{1}" -f $config.ClinicApi.BaseUrl.TrimEnd('/'), $config.ClinicApi.ImportEndpoint.TrimStart('/')

Write-Host ("ConfigPath  : {0}" -f $ConnectorConfigPath)
Write-Host ("OutboxPath  : {0}" -f $queuePath)
Write-Host ("API URL     : {0}" -f $importUrl)
Write-Host ("API Header  : {0}" -f $config.ClinicApi.ApiKeyHeaderName)
Write-Host ("Source      : {0}" -f $config.ClinicApi.ConnectorSource)

$sendSucceeded = $false
$sendError = $null

Write-Section "Listener Check"
$listenerOk = Test-TcpEndpoint -HostName $ConnectorHost -PortNumber $ConnectorPort
Write-Host ("TCP {0}:{1} reachable: {2}" -f $ConnectorHost, $ConnectorPort, $listenerOk)
try {
    $listenEntries = Get-NetTCPConnection -State Listen -LocalPort $ConnectorPort -ErrorAction Stop
    if ($listenEntries) {
        $listenEntries | Select-Object -First 5 LocalAddress, LocalPort, OwningProcess | ForEach-Object {
            Write-Host ("Listener process: PID {0} on {1}:{2}" -f $_.OwningProcess, $_.LocalAddress, $_.LocalPort)
        }
    }
    else {
        Write-Host "No LISTEN socket found for this port." -ForegroundColor Yellow
    }
}
catch {
    Write-Host ("Could not inspect listening sockets: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
}

if ($SendTest) {
    Write-Section "Send Test Payload"
    try {
        $payloadSent = Send-Hl7Payload -HostName $ConnectorHost -PortNumber $ConnectorPort -PatientSampleId $SampleId -Code $TestCode -Value $ResultValue
        $sendSucceeded = $true
        Write-Host "Payload sent to connector listener."
        Write-Host $payloadSent
    }
    catch {
        $sendError = $_.Exception.Message
        Write-Host ("Send failed: {0}" -f $sendError) -ForegroundColor Red
    }
}

if ($WaitSeconds -gt 0) {
    Start-Sleep -Seconds $WaitSeconds
}

Write-Section "Outbox Check"
$entries = @()
if (Test-Path $queuePath) {
    $raw = Get-Content $queuePath -Raw
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        $parsed = $raw | ConvertFrom-Json
        if ($parsed -is [System.Array]) {
            $entries = $parsed
        }
        elseif ($null -ne $parsed) {
            $entries = @($parsed)
        }
    }
}
else {
    Write-Host "Outbox file does not exist yet."
}

if ($queuePath -ne "C:\Windows\System32\connector-outbox.json" -and (Test-Path "C:\Windows\System32\connector-outbox.json")) {
    $legacyInfo = Get-Item "C:\Windows\System32\connector-outbox.json"
    Write-Host ("Legacy outbox detected at C:\Windows\System32\connector-outbox.json (LastWrite={0})" -f $legacyInfo.LastWriteTime) -ForegroundColor Yellow
}

if ($entries.Count -eq 0) {
    Write-Host "No pending outbox items."
}
else {
    $targetEntries = @($entries | Where-Object {
            $_.Payload.PatientIdentifier -eq $SampleId -and $_.Payload.TestCode -eq $TestCode
        })

    if ($targetEntries.Count -eq 0) {
        Write-Host "No outbox item found for SampleId=$SampleId and TestCode=$TestCode."
        Write-Host "Latest 3 outbox items:"
        $entries | Select-Object -Last 3 | ForEach-Object {
            Write-Host ("- {0} | {1} | {2} | Attempts={3} | LastError={4}" -f $_.Id, $_.Payload.PatientIdentifier, $_.Payload.TestCode, $_.AttemptCount, $_.LastError)
        }
    }
    else {
        Write-Host ("Found {0} matching outbox item(s)." -f $targetEntries.Count) -ForegroundColor Yellow
        $targetEntries | ForEach-Object {
            Write-Host ("- Id={0}" -f $_.Id)
            Write-Host ("  AttemptCount={0}" -f $_.AttemptCount)
            Write-Host ("  NextAttemptAtUtc={0}" -f $_.NextAttemptAtUtc)
            Write-Host ("  LastError={0}" -f $_.LastError)
        }
    }
}

Write-Section "API Reachability"
$requestParams = @{
    Uri        = $importUrl
    Method     = "Get"
    TimeoutSec = 12
}

try {
    $response = Invoke-WebRequestCompat -Params $requestParams -UseSkipCertificateCheck:$SkipCertificateCheck
    Write-Host ("API reachable, status: {0}" -f $response.StatusCode) -ForegroundColor Green
}
catch {
    $statusCode = $null
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
        $statusCode = [int]$_.Exception.Response.StatusCode
    }

    if ($statusCode -ne $null) {
        Write-Host ("API reachable but returned status: {0}" -f $statusCode) -ForegroundColor Yellow
    }
    else {
        Write-Host ("API not reachable: {0}" -f $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Section "Recent Service Events"
try {
    $recentEvents = Get-WinEvent -LogName Application -MaxEvents 200 |
        Where-Object {
            $_.ProviderName -eq $ConnectorServiceName -or
            $_.ProviderName -eq ".NET Runtime" -and (
                $_.Message -like "*LabConnector.Worker*" -or
                $_.Message -like "*NagmLabConnector*" -or
                $_.Message -like "*TCP Listener*" -or
                $_.Message -like "*outbox*"
            )
        } |
        Select-Object -First 8 TimeCreated, Id, LevelDisplayName, Message

    if ($recentEvents.Count -eq 0) {
        Write-Host "No recent Application log entries for provider $ConnectorServiceName."
    }
    else {
        $recentEvents | ForEach-Object {
            Write-Host ("[{0}] {1} | {2}" -f $_.TimeCreated, $_.LevelDisplayName, $_.Message)
        }
    }
}
catch {
    Write-Host ("Could not read event log: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
}

if ($DirectApiCheck) {
    Write-Section "Direct API Import Check"
    $bodyObject = @{
        ConnectorSource = "DEBUG-SCRIPT"
        Results         = @(
            @{
                DeviceId          = "EC38"
                PatientIdentifier = $SampleId
                TestCode          = $TestCode
                ResultValue       = $ResultValue
                Unit              = $Unit
                Timestamp         = [DateTime]::UtcNow.ToString("o")
                RawPayload        = "DEBUG-SCRIPT"
            }
        )
    }

    $headers = @{}
    $headerName = $config.ClinicApi.ApiKeyHeaderName
    if (-not [string]::IsNullOrWhiteSpace($headerName) -and -not [string]::IsNullOrWhiteSpace($config.ClinicApi.ApiKey)) {
        $headers[$headerName] = $config.ClinicApi.ApiKey
    }

    $postParams = @{
        Uri         = $importUrl
        Method      = "Post"
        TimeoutSec  = 15
        ContentType = "application/json"
        Body        = ($bodyObject | ConvertTo-Json -Depth 8)
        Headers     = $headers
    }

    try {
        $importResponse = Invoke-RestMethodCompat -Params $postParams -UseSkipCertificateCheck:$SkipCertificateCheck
        Write-Host "Direct API import succeeded." -ForegroundColor Green
        Write-Host ($importResponse | ConvertTo-Json -Depth 10)
    }
    catch {
        Write-Host ("Direct API import failed: {0}" -f $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Section "Diagnosis"
if ($entries.Count -gt 0) {
    $badEndpointEntries = @($entries | Where-Object { $_.LastError -like "*localhost:5001*" })
    if ($badEndpointEntries.Count -gt 0 -and $importUrl -notlike "*localhost:5001*") {
        Write-Host "Connector is still using old API endpoint (localhost:5001) at runtime." -ForegroundColor Red
        Write-Host "Action: restart service '$ConnectorServiceName' from elevated PowerShell or Services app."
        if (-not $listenerOk) {
            Write-Host "Likely root cause: service started with default config (appsettings not loaded), so it uses fallback endpoint and no device listeners." -ForegroundColor Red
            Write-Host "Suggested fix in worker Program.cs: set configuration base path to AppContext.BaseDirectory before binding options." -ForegroundColor Yellow
        }
    }
}

if (-not $listenerOk) {
    Write-Host "Connector listener is not reachable on $ConnectorHost`:$ConnectorPort." -ForegroundColor Red
}

if ($SendTest -and -not $sendSucceeded -and $sendError) {
    Write-Host ("Test frame was not accepted by connector listener: {0}" -f $sendError) -ForegroundColor Red
}

Write-Host "Debug run completed."
