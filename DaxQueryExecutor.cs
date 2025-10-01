Hi Satya,

I didn’t manage to connect with Ramesh, but I believe he was referring to scenarios where populating a filter list is slow when using a DirectQuery attribute. In such cases, the SQL query executed against the detail table retrieves distinct values without any filter, which results in scanning the entire table and slows down performance. This is consistent with the DQ issue (case 2) we raised with Microsoft.

As we discussed last week, one option is to create an intermediate aggregation table containing a second level of attributes. This would significantly improve performance because of the smaller table size.

Another approach could be moving these attributes into a separate table running in DirectQuery mode. The drawback is that it may require additional joins, which could affect query performance.

Let’s review this in tomorrow’s call.

Hi Amar/All,

Since this behavior is already known to Microsoft and considered as working as designed, and given that even if Microsoft were to make a change it would likely take significant time, we need to continue testing the model with more advanced scenarios. We will run benchmarks that include complex calculations with MEVs, combining in-memory and DirectQuery attributes, all executed in DirectQuery mode in Databricks. This will help validate the DQ solution and reduce the risk of encountering the same issue in the future.

I will work with Abhishek to identify the most complex queries Marvel is currently using and will incorporate additional DAX into the benchmarks. If these benchmarks are successful, they will provide a stronger level of confidence for future scenarios.

Thanks,
Julio on




<#
.SYNOPSIS
  Monitor a Power BI dataset parameter and highlight changes.

.PREREQS
  1) PowerShell 7+ recommended (works on Windows PowerShell too).
  2) Install modules (once): 
       Install-Module MicrosoftPowerBIMgmt -Scope CurrentUser
  3) You must have permission to read the dataset in the workspace.

.USAGE
  - Set $WorkspaceName, $DatasetName, $ParameterName below.
  - Run the script. A browser/MSAL prompt will sign you in.
  - Press Ctrl+C to stop.
#>

# ----------- CONFIGURE THESE -----------
$WorkspaceName = "My Workspace Name"
$DatasetName   = "My Dataset Name"
$ParameterName = "MyParameter"  # exactly as shown in Power BI
$PollSeconds   = 30
# --------------------------------------

Import-Module MicrosoftPowerBIMgmt.Profile  -ErrorAction Stop
Import-Module MicrosoftPowerBIMgmt.Workspaces -ErrorAction Stop
Import-Module MicrosoftPowerBIMgmt.Data   -ErrorAction Stop

function Connect-PowerBI-IfNeeded {
    try {
        # If token is stale, this will prompt you again as needed
        if (-not (Get-PowerBIAccessToken -ErrorAction SilentlyContinue)) {
            Connect-PowerBIServiceAccount | Out-Null
        }
    } catch {
        Write-Host "Sign-in required..." -ForegroundColor Yellow
        Connect-PowerBIServiceAccount | Out-Null
    }
}

function Resolve-WorkspaceId {
    param([string]$Name)
    $ws = Get-PowerBIWorkspace -Name $Name -All | Select-Object -First 1
    if (-not $ws) { throw "Workspace '$Name' not found or not visible." }
    return $ws.Id
}

function Resolve-DatasetId {
    param([Guid]$WorkspaceId, [string]$Name)
    $ds = Get-PowerBIDataset -WorkspaceId $WorkspaceId | Where-Object { $_.Name -eq $Name } | Select-Object -First 1
    if (-not $ds) { throw "Dataset '$Name' not found in workspace." }
    return $ds.Id
}

function Get-ParameterValue {
    param([Guid]$WorkspaceId, [string]$DatasetId, [string]$ParameterName)

    $url = "groups/$WorkspaceId/datasets/$DatasetId/parameters"
    # Uses your user context to call the REST API
    $raw = Invoke-PowerBIRestMethod -Url $url -Method Get
    $obj = $raw | ConvertFrom-Json

    $param = $obj.value | Where-Object { $_.name -eq $ParameterName }
    if (-not $param) { throw "Parameter '$ParameterName' not found in dataset." }
    return $param.currentValue
}

# ---------- MAIN ----------
try {
    Connect-PowerBI-IfNeeded

    $workspaceId = Resolve-WorkspaceId -Name $WorkspaceName
    $datasetId   = Resolve-DatasetId   -WorkspaceId $workspaceId -Name $DatasetName

    Write-Host "Monitoring parameter '$ParameterName' in dataset '$DatasetName' (workspace '$WorkspaceName')" -ForegroundColor Cyan
    Write-Host "Polling every $PollSeconds seconds. Press Ctrl+C to stop." -ForegroundColor DarkCyan

    $lastValue = $null

    while ($true) {
        try {
            Connect-PowerBI-IfNeeded
            $current = Get-ParameterValue -WorkspaceId $workspaceId -DatasetId $datasetId -ParameterName $ParameterName
            $ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

            if ($null -eq $lastValue) {
                Write-Host "[$ts] $ParameterName = $current"
            }
            elseif ($current -ne $lastValue) {
                Write-Host "[$ts] $ParameterName changed: '$lastValue' -> '$current'" -ForegroundColor Yellow
                try { [console]::Beep(1000,200) } catch {}
            }
            else {
                Write-Host "[$ts] unchanged ($current)" -ForegroundColor DarkGray
            }

            $lastValue = $current
        }
        catch {
            Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: $($_.Exception.Message)" -ForegroundColor Red
        }

        Start-Sleep -Seconds $PollSeconds
    }
}
catch {
    Write-Host "Startup failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}



