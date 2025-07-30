Add-Type -AssemblyName System.Collections.Concurrent

$scanPath = "C:\YourAppFolder"   # 🔁 Change this
$outputCsv = "C:\temp\dotnet5_usage_report.csv"
$results = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

Write-Host "🔍 Collecting files..."
$allFiles = Get-ChildItem -Path $scanPath -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { ($_.Extension -eq ".dll" -or $_.Extension -eq ".exe") -and $_.FullName.Length -lt 260 }

# Force PowerShell to use correct ForEach overload by casting the list
$typedFiles = [System.Collections.Generic.List[object]]::new()
$allFiles | ForEach-Object { $typedFiles.Add($_) }

$totalFiles = $typedFiles.Count
$script:counter = 0

Write-Host "⚙️ Scanning $totalFiles files for .NET 5.0 references..."

[System.Threading.Tasks.Parallel]::ForEach($typedFiles, [Action[object]]{
    param ($file)

    try {
        $path = $file.FullName
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $maxRead = [Math]::Min(4096, $bytes.Length)
        $text = [System.Text.Encoding]::ASCII.GetString($bytes[0..($maxRead - 1)])

        if ($text -match "net5.0") {
            $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path)
            $results.Add([PSCustomObject]@{
                FilePath        = $path
                ProductName     = $fvi.ProductName
                FileVersion     = $fvi.FileVersion
                TargetFramework = "net5.0"
            })
        }
    } catch { }

    $current = [System.Threading.Interlocked]::Increment([ref]$script:counter)
    if ($current % 50 -eq 0 -or $current -eq $totalFiles) {
        $percent = [Math]::Round(($current / $totalFiles) * 100, 2)
        Write-Progress -Activity "Scanning .NET assemblies" -Status "$current of $totalFiles scanned..." -PercentComplete $percent
    }
})

Write-Progress -Activity "Scanning .NET assemblies" -Completed -Status "Done"
$results | Export-Csv -Path $outputCsv -NoTypeInformation -Encoding UTF8
Write-Host "`n✅ Scan complete. Results saved to $outputCsv"
