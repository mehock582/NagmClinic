# Lab Connector Setup Guide (Lab PC)

## 1) What This Setup Does

On the lab PC:

1. Connector app listens for analyzer/device data (TCP/COM/File).
2. Connector normalizes results.
3. Connector posts results to Clinic API.

Data flow:

`Lab Device -> LabConnector.Worker -> Clinic API (/api/lab-results/import)`

---

## 2) Clinic API Link It Posts To

Current configured target:

`https://localhost:44356/api/lab-results/import`

From connector config:

- `ClinicApi.BaseUrl = https://localhost:44356`
- `ClinicApi.ImportEndpoint = /api/lab-results/import`

Final URL is:

`{BaseUrl}{ImportEndpoint}`

---

## 3) Required on Lab PC

1. Clinic web app reachable on configured URL.
2. Folder exists: `C:\LabConnector`
3. Windows service name: `NagmLabConnector`
4. Connector config file: `C:\LabConnector\appsettings.json`

---

## 4) Connector Config Example (`C:\LabConnector\appsettings.json`)

```json
{
  "ClinicApi": {
    "BaseUrl": "https://localhost:44356",
    "ImportEndpoint": "/api/lab-results/import",
    "ApiKeyHeaderName": "X-Connector-Api-Key",
    "ApiKey": "CHANGE-THIS-CONNECTOR-KEY",
    "ConnectorSource": "LAB-PC-01",
    "AllowInvalidHttpsCertificate": true
  },
  "ConnectorDispatch": {
    "QueueFilePath": "connector-outbox.json",
    "MaxRetryAttempts": 10,
    "RetryBaseDelaySeconds": 5,
    "BatchSize": 50,
    "FlushIntervalSeconds": 5
  },
  "LabDevices": [
    {
      "DeviceId": "EC38",
      "Name": "Bioelab EC-38",
      "ConnectionType": "LAN",
      "Port": 5000,
      "ProtocolType": "HL7",
      "IsActive": true
    }
  ]
}
```

---

## 5) Deploy / Update Commands

Run as Administrator PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\le\source\repos\NagmClinic\NagmClinic\scripts\Deploy-ConnectorFix.ps1"
```

What it does:

1. Stops service `NagmLabConnector`
2. Copies fixed build from `C:\LabConnector\_staged_fix` to `C:\LabConnector`
3. Starts service again
4. Checks listener and endpoint

---

## 6) Service Commands (Admin PowerShell)

```powershell
Get-Service NagmLabConnector
Start-Service NagmLabConnector
Stop-Service NagmLabConnector
Restart-Service NagmLabConnector
```

---

## 7) Test Commands

### A) Send one custom result

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\le\source\repos\NagmClinic\NagmClinic\scripts\Send-ConnectorTestData.ps1" -TargetHost 127.0.0.1 -Port 5000 -Protocol HL7 -SampleId LAB-20260411-26041102 -Tests "TT=24.8"
```

### B) Use general test runner (recommended)

Abnormal test values:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\le\source\repos\NagmClinic\NagmClinic\scripts\Run-LabConnectorTest.ps1" -VisitNumber 26041102 -TestCodes "PSA,HFABP,TT" -Mode Abnormal
```

Normal values:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\le\source\repos\NagmClinic\NagmClinic\scripts\Run-LabConnectorTest.ps1" -VisitNumber 26041102 -TestCodes "PSA,HFABP,TT" -Mode Normal
```

### C) Full debug

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\le\source\repos\NagmClinic\NagmClinic\scripts\Debug-ConnectorFlow.ps1" -SendTest -SampleId LAB-20260411-26041102 -TestCode TT -ResultValue 24.8 -SkipCertificateCheck
```

---

## 8) What You Must Match for Successful Posting

1. `SampleId` must map to visit (example: `LAB-YYYYMMDD-VisitNumber`).
2. `TestCode` must match requested test code or configured mapping.
3. Connector service must be running.
4. Clinic app endpoint must be reachable.

---

## 9) Quick Troubleshooting

1. Service running but no posting:
   - Check `C:\LabConnector\connector-outbox.json`
2. SSL error:
   - Keep `AllowInvalidHttpsCertificate: true` for local dev only.
3. Results not appearing in lab screen:
   - Verify visit SampleId and test code.
   - Verify test is requested in that appointment.
4. Old data path issue:
   - If old file exists at `C:\Windows\System32\connector-outbox.json`, run deploy script again and retest.

---

## 10) For Production

Change:

1. `ClinicApi.BaseUrl` to real clinic domain/IP.
2. `AllowInvalidHttpsCertificate` to `false`.
3. `ApiKey` to secure real key.

