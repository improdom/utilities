Add-Type -AssemblyName System.Collections.Concurrent

$scanPath = "C:\YourAppFolder"  # ✅ CHANGE this to your folder
$outputCsv = "C:\temp\dotnet5_usage_report.csv"
$results = New-Object System.Collections.Concurrent.ConcurrentBag[psobject]

# Use a thread-safe counter object
$counter = New-Object -TypeName PSObject -Property @{ Value = 0 }

Write-Host "`n🔍 Collecting .dll and .exe files under $scanPath..."
$allFiles = Get-ChildItem -Path $scanPath -Recurse -ErrorAction SilentlyContinue |
    Where-Object { ($_.Extension -eq ".dll" -or $_.Extension -eq ".exe") -and $_.FullName.Length -lt 260 }

$totalFiles = $allFiles.Count
Write-Host "⚙️ Found $totalFiles files to scan."

# Start progress in background job (for UI)
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

# Manually parallelize using Runspaces
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
                    TargetFramework = "net5.0"
                })
            }
        } catch {}

        $refCounter.Value++
    }).AddArgument($file).AddArgument($results).AddArgument($counter)
    $runspace.RunspacePool = $runspacePool
    $null = $runspace.BeginInvoke()
    $runspaces += $runspace
}

# Wait for all to finish
$runspaces | ForEach-Object { $_.EndInvoke($_.BeginInvoke()) }

# Cleanup progress job
Stop-Job $progressJob | Out-Null
Remove-Job $progressJob | Out-Null

# Export results
$results | Export-Csv -Path $outputCsv -NoTypeInformation -Encoding UTF8
Write-Host "`n✅ Scan complete. Results saved to $outputCsv"
