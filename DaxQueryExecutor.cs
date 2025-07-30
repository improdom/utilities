# Define the root path to scan (update if needed)
$scanPath = "C:\"

# Output CSV path
$outputCsv = "C:\dotnet5_usage_report.csv"

# Create a list to store results
$results = @()

# Get all .dll and .exe files under the path
Get-ChildItem -Path $scanPath -Recurse -Include *.dll, *.exe -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $assemblyPath = $_.FullName

        # Load the assembly metadata (without executing it)
        $assembly = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)

        # Read custom attributes from the assembly
        $peReader = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbFile($assemblyPath)
        $reader = $peReader.GetMetadataReader()
        
    } catch {
        # Fallback to use System.Diagnostics.FileVersionInfo
        $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath)

        # Read the raw file for the TargetFrameworkAttribute
        $rawText = [System.IO.File]::ReadAllText($assemblyPath) -replace "`0", ""
        if ($rawText -match "net5.0") {
            $results += [PSCustomObject]@{
                FilePath     = $assemblyPath
                ProductName  = $fvi.ProductName
                FileVersion  = $fvi.FileVersion
                TargetFramework = "net5.0"
            }
        }
    }
}

# Export results
$results | Export-Csv -Path $outputCsv -NoTypeInformation -Encoding UTF8

Write-Host "Scan complete. Results saved to $outputCsv"
