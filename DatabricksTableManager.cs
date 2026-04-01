# Root folder of your cloned GitLab repo
$repoRoot = "C:\Repos\Mars"

# Output CSV
$outputCsv = Join-Path $repoRoot "kendo_inventory_detailed.csv"

# File types to scan
$fileExtensions = @(
    ".cshtml", ".aspx", ".ascx", ".html",
    ".js", ".ts",
    ".csproj", ".config", ".json"
)

# General Kendo patterns
$generalPatterns = @(
    'Html\.Kendo\(\)',
    '\.kendo[A-Za-z0-9_]+\(',
    'Kendo\.Mvc',
    'kendo\.all',
    'kendo\.common',
    'kendo\.default',
    '@progress/kendo-ui',
    'data-role\s*=\s*"[^"]+"'
)

# Known Kendo controls to identify
$controlPatterns = @{
    "Grid"              = 'Html\.Kendo\(\)\.Grid|\.kendoGrid\(|data-role\s*=\s*"grid"'
    "DropDownList"      = 'Html\.Kendo\(\)\.DropDownList|\.kendoDropDownList\(|data-role\s*=\s*"dropdownlist"'
    "ComboBox"          = 'Html\.Kendo\(\)\.ComboBox|\.kendoComboBox\(|data-role\s*=\s*"combobox"'
    "MultiSelect"       = 'Html\.Kendo\(\)\.MultiSelect|\.kendoMultiSelect\(|data-role\s*=\s*"multiselect"'
    "DatePicker"        = 'Html\.Kendo\(\)\.DatePicker|\.kendoDatePicker\(|data-role\s*=\s*"datepicker"'
    "DateTimePicker"    = 'Html\.Kendo\(\)\.DateTimePicker|\.kendoDateTimePicker\(|data-role\s*=\s*"datetimepicker"'
    "TimePicker"        = 'Html\.Kendo\(\)\.TimePicker|\.kendoTimePicker\(|data-role\s*=\s*"timepicker"'
    "NumericTextBox"    = 'Html\.Kendo\(\)\.NumericTextBox|\.kendoNumericTextBox\(|data-role\s*=\s*"numerictextbox"'
    "Window"            = 'Html\.Kendo\(\)\.Window|\.kendoWindow\(|data-role\s*=\s*"window"'
    "Editor"            = 'Html\.Kendo\(\)\.Editor|\.kendoEditor\(|data-role\s*=\s*"editor"'
    "TabStrip"          = 'Html\.Kendo\(\)\.TabStrip|\.kendoTabStrip\(|data-role\s*=\s*"tabstrip"'
    "TreeView"          = 'Html\.Kendo\(\)\.TreeView|\.kendoTreeView\(|data-role\s*=\s*"treeview"'
    "Upload"            = 'Html\.Kendo\(\)\.Upload|\.kendoUpload\(|data-role\s*=\s*"upload"'
    "Chart"             = 'Html\.Kendo\(\)\.Chart|\.kendoChart\(|data-role\s*=\s*"chart"'
    "Scheduler"         = 'Html\.Kendo\(\)\.Scheduler|\.kendoScheduler\(|data-role\s*=\s*"scheduler"'
    "ListView"          = 'Html\.Kendo\(\)\.ListView|\.kendoListView\(|data-role\s*=\s*"listview"'
    "AutoComplete"      = 'Html\.Kendo\(\)\.AutoComplete|\.kendoAutoComplete\(|data-role\s*=\s*"autocomplete"'
    "PanelBar"          = 'Html\.Kendo\(\)\.PanelBar|\.kendoPanelBar\(|data-role\s*=\s*"panelbar"'
    "Menu"              = 'Html\.Kendo\(\)\.Menu|\.kendoMenu\(|data-role\s*=\s*"menu"'
    "Tooltip"           = 'Html\.Kendo\(\)\.Tooltip|\.kendoTooltip\(|data-role\s*=\s*"tooltip"'
    "Splitter"          = 'Html\.Kendo\(\)\.Splitter|\.kendoSplitter\(|data-role\s*=\s*"splitter"'
    "Dialog"            = 'Html\.Kendo\(\)\.Dialog|\.kendoDialog\(|data-role\s*=\s*"dialog"'
    "Validator"         = 'Html\.Kendo\(\)\.Validator|\.kendoValidator\('
    "Notification"      = 'Html\.Kendo\(\)\.Notification|\.kendoNotification\('
    "MaskedTextBox"     = 'Html\.Kendo\(\)\.MaskedTextBox|\.kendoMaskedTextBox\('
    "DropDownTree"      = 'Html\.Kendo\(\)\.DropDownTree|\.kendoDropDownTree\('
    "PivotGrid"         = 'Html\.Kendo\(\)\.PivotGrid|\.kendoPivotGrid\('
}

$results = @()

# Find files to scan
$files = Get-ChildItem -Path $repoRoot -Recurse -File | Where-Object {
    $fileExtensions -contains $_.Extension.ToLower()
}

foreach ($file in $files) {
    $relativePath = $file.FullName.Replace($repoRoot, "").TrimStart('\')

    # Try to infer project from first folder or nearest csproj
    $projectName = ""
    $projectFile = Get-ChildItem -Path $file.DirectoryName -Filter *.csproj -File -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($projectFile) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile.Name)
    }
    else {
        $parts = $relativePath -split '[\\/]'
        if ($parts.Count -gt 0) {
            $projectName = $parts[0]
        }
    }

    $matches = Select-String -Path $file.FullName -Pattern $generalPatterns -AllMatches

    foreach ($match in $matches) {
        $lineText = $match.Line.Trim()
        $detectedControls = @()

        foreach ($controlName in $controlPatterns.Keys) {
            if ($lineText -match $controlPatterns[$controlName]) {
                $detectedControls += $controlName
            }
        }

        # Infer usage type
        $usageType = "General Reference"
        if ($lineText -match 'Html\.Kendo\(\)') {
            $usageType = "MVC Wrapper"
        }
        elseif ($lineText -match '\.kendo[A-Za-z0-9_]+\(') {
            $usageType = "JavaScript Initialization"
        }
        elseif ($lineText -match 'data-role\s*=') {
            $usageType = "Markup Data Role"
        }
        elseif ($lineText -match 'Kendo\.Mvc|@progress/kendo-ui|kendo\.all|kendo\.common|kendo\.default') {
            $usageType = "Library Reference"
        }

        if ($detectedControls.Count -eq 0) {
            $detectedControls = @("Unknown / General Kendo Reference")
        }

        foreach ($control in $detectedControls) {
            $results += [PSCustomObject]@{
                Project       = $projectName
                FilePath      = $relativePath
                FileName      = $file.Name
                Extension     = $file.Extension
                LineNumber    = $match.LineNumber
                UsageType     = $usageType
                Control       = $control
                MatchedLine   = $lineText
            }
        }
    }
}

# Remove duplicates
$results = $results | Sort-Object Project, FilePath, LineNumber, Control -Unique

# Export CSV
$results | Export-Csv -Path $outputCsv -NoTypeInformation -Encoding UTF8

Write-Host "Scan complete. Results exported to: $outputCsv"
Write-Host "Total findings: $($results.Count)"



$summaryCsv = Join-Path $repoRoot "kendo_inventory_summary.csv"

$results |
    Group-Object Project, FilePath |
    ForEach-Object {
        $first = $_.Group | Select-Object -First 1
        [PSCustomObject]@{
            Project    = $first.Project
            FilePath   = $first.FilePath
            FileName   = $first.FileName
            UsageTypes = ($_.Group.UsageType | Sort-Object -Unique) -join "; "
            Controls   = ($_.Group.Control | Sort-Object -Unique) -join ", "
            Hits       = $_.Count
        }
    } |
    Sort-Object Project, FilePath |
    Export-Csv -Path $summaryCsv -NoTypeInformation -Encoding UTF8

Write-Host "Summary exported to: $summaryCsv"
