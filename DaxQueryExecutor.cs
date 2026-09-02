
using System.Text.RegularExpressions;

string input = "ABC,DEF, GHI,JKL";

string[] values = Regex.Split(input, @",(?! )");



measures:

  - name: Converted Reporting Value
    expr: >
      CASE
        WHEN COUNT(DISTINCT report_currency.currency) = 1
        THEN SUM(
          source.reporting_value * report_currency.spot_value
        )
        ELSE SUM(
          CASE
            WHEN report_currency.currency = 'USD'
            THEN source.reporting_value * report_currency.spot_value
            ELSE 0
          END
        )

  - name: EQ Delta Gross UBSInstID
    expr: MEASURE(`Converted Reporting Value`)






#!/usr/bin/env pwsh

# Fetch Secrets script for MR Marvel API

$TenantId      = "fb6ea403-7cf1-4905-810a-fe5547e98204"
$SubscriptionId = "a4651752-c062-4446-9a4a-d0faed180ed1"
$VaultName     = "akv-mtrc-neu-dev-shrd"
$OutputDir     = "/home/devpod/Config"

# Create the output directory when it does not exist
New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null

# Check whether Azure CLI is authenticated
$accountJson = az account show --output json 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Not authenticated. Running az login..."

    az config set core.login_experience_v2=off

    az login `
        --allow-no-subscriptions `
        --tenant $TenantId `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Azure login failed."
    }

    Write-Host "Login successful."

    $accountJson = az account show --output json 2>$null
}

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accountJson)) {
    throw "Unable to retrieve Azure account information."
}

$account = $accountJson | ConvertFrom-Json

Write-Host "Logged in as $($account.user.name)"
Write-Host "Tenant ID: $($account.tenantId)"
Write-Host "Subscription ID: $($account.id)"
Write-Host "Vault Name: $VaultName"
Write-Host "Output directory: $OutputDir"

# Select the required Azure subscription
az account set --subscription $SubscriptionId

if ($LASTEXITCODE -ne 0) {
    throw "Unable to select subscription '$SubscriptionId'."
}

# Get the names of all enabled secrets
$secretNames = @(
    az keyvault secret list `
        --vault-name $VaultName `
        --query "[?attributes.enabled==``true``].name" `
        --output tsv
)

if ($LASTEXITCODE -ne 0) {
    throw "Unable to retrieve secrets from Key Vault '$VaultName'."
}

# Remove empty output lines
$secretNames = $secretNames |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Write-Host "Found $($secretNames.Count) enabled secrets."

# Download up to 25 secrets concurrently
$secretNames | ForEach-Object -Parallel {
    $secretName = $_

    try {
        $secretValue = az keyvault secret show `
            --vault-name $using:VaultName `
            --name $secretName `
            --query "value" `
            --output tsv 2>$null

        if ($LASTEXITCODE -ne 0) {
            throw "Azure CLI returned exit code $LASTEXITCODE."
        }

        $destinationPath = Join-Path `
            -Path $using:OutputDir `
            -ChildPath $secretName

        # Write the value without adding a newline
        [System.IO.File]::WriteAllText(
            $destinationPath,
            [string]$secretValue
        )

        Write-Host "Written: $secretName"
    }
    catch {
        Write-Error "Failed to retrieve secret '$secretName': $($_.Exception.Message)"
    }
} -ThrottleLimit 25

Write-Host "Done."





Hi Michal,

Thanks for the clarification and for pointing me to the communications and instructions. I wasn’t aware this was the root cause, but we’ll update our repositories accordingly.

Thanks for your help.

Regards,
Julio






I need to write an c#?application which will run as a job at night at a scheduled time.
This application responsibility to synchronize reports in pbir format currently deployed in power bi and service and similar files stored in ADLS.
Basically, users will generate a report from an application named self-service, they will select the semantic model they want to run their reports on, apply filters, select attributes, etc. when they click on save a report definition in pbir is generated, published to power bi in a PPL environment, and files also uploaded to ADLS for tracking and traceability.
There is a workflow in the application from where reports can be approved and then moved to Prod environment.
In PPL users can make changes in report online, which will bring the report in the power bi service out of sync with the last version in ADLS.

The job will read all active and available reports from SQL Mi table, it will then download the pbir files from ADLS and also from power bi ppl workspace, it will compare each one of   the set of files for differences, if you identify a difference then it will update ADLS with the changes currently in the power bi service so both sources are un sync.
I need your help preparing the requirements, please ask any relevant questionPBIR Report Publishing with ADLS Version Management





I've completed a prototype and validated the core functionality for managing PBIR file versioning using native Azure Blob Versioning. The prototype supports uploading, updating, downloading, restoring previous versions, and handling complete PBIR project structures while preserving folder hierarchy
