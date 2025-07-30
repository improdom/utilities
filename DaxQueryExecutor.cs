# Requires PowerShell 7 or later
$scanPath = "C:\"
$outputCsv = "C:\dotnet5_usage_report.csv"

# Collect all .dll and .exe files first
$files = Get-ChildItem -Path $scanPath -Recurse -Include *.dll, *.exe -File -ErrorAction SilentlyContinue

# Thread-safe result collection
$results = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

$files | ForEach-Object -Parallel {
    param ($file, $bag)

    try {
        $path = $file.FullName
        $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path)

        # Check if file contains net5.0 string (indicates target framework)
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $text = [System.Text.Encoding]::ASCII.GetString($bytes)
        
        if ($text -match "net5.0") {
            $bag.Add([PSCustomObject]@{
                FilePath        = $path
                ProductName     = $fvi.ProductName
                FileVersion     = $fvi.FileVersion
                TargetFramework = "net5.0"
            })
        }
    } catch {
        # Ignore unreadable files
    }
} -ArgumentList $_, $results -ThrottleLimit 8

# Export result
$results | Export-Csv -Path $outputCsv -NoTypeInformation -Encoding UTF8

Write-Host "Scan complete. Results saved to $outputCsv"
