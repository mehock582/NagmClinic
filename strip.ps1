$files = Get-ChildItem -Path "c:\Users\le\source\repos\NagmClinic\NagmClinic\Views" -Filter "*.cshtml" -Recurse
foreach ($f in $files) {
    $c = Get-Content $f.FullName -Raw
    $n = $c.Replace('<link rel="stylesheet" href="https://cdn.datatables.net/1.13.7/css/dataTables.bootstrap5.min.css" />', '').Replace('<script src="https://cdn.datatables.net/1.13.7/js/jquery.dataTables.min.js"></script>', '').Replace('<script src="https://cdn.datatables.net/1.13.7/js/dataTables.bootstrap5.min.js"></script>', '')
    if ($n -ne $c) {
        Set-Content -Path $f.FullName -Value $n
        Write-Host "Updated $($f.FullName)"
    }
}
