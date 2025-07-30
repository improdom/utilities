Add-Type -AssemblyName System.Collections.Concurrent

$scanPath = "C:\"   # 🔁 Scan everything under C:\ except C:\Users
$excludePath = "C:\\Users"
$outputCsv = "C:\temp\dotnet5_usage_report.csv"
$results = New-Object System.Collections.Concurrent.ConcurrentBag[psobject]

# Thread-safe counter
$counter = New-Object -TypeName PSObject -Property @{ Value = 0 }

Write-Host "`n🔍 Collecting .dll and .exe files under $scanPath (excluding $excludePath)..."

# Get all .dll and .exe files, excluding those inside C:\Users
$allFiles = Get-ChildItem -Path $scanPath -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        ($_.Extension -eq ".dll" -or $_.Extension -eq ".exe") -and
        $_.FullName.Length -lt 260 -and
        ($_.FullName -notlike "$excludePath*")
    }

$totalFiles = $allFiles.Count
Write-Host "⚙️ Found $totalFiles files to scan."

# Start a background job to show progress bar
$progressJob = Start-Job -ScriptBlock {
    param($total, $refCounter)
    do {
        Start-Sleep -Milliseconds 500
        $count = $refCounter.Value
        $percent = [math]::Round(($count / $total) * 100, 2)
        Write-Progress -Activity "Scanning for .NET 5.0" -Status "$count of $total files scanned..." -PercentComplete $percent
    } while ($count -lt $total)
    Write-Progress -Activity "Scanning for .NET 5.0" -Completed
} -ArgumentList $totalFiles, $counter

# Use runspaces for multithreading
$runspacePool = [runspacefactory]::CreateRunspacePool(1, 6)
$runspacePool.Open()
$runspaces = @()

foreach ($file in $allFiles) {
    $runspace = [powershell]::Create().AddScript({
        param($file, $resultsBag, $refCounter)
        try {
            $path = $file.FullName
            $bytes = [System.IO.File]::ReadAllBytes($path)
            $maxRead = [Math]::Min(4096, $bytes.Length)
            $text = [System.Text.Encoding]::ASCII.GetString($bytes[0..($maxRead - 1)])

            if ($text -match "net5.0") {
                $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path)
                $resultsBag.Add([pscustomobject]@{
                    FilePath        = $path
                    ProductName     = $fvi.ProductName
                    FileVersion     = $fvi.FileVersion
                    TargetFrame
