<# =======================  PARAMETERS  ======================= #>
param(
  # Auth (Service Principal is recommended for CI/CD)
  [Parameter(Mandatory=$true)] [string] $TenantId,
  [Parameter(Mandatory=$true)] [string] $ClientId,
  [Parameter(Mandatory=$true)] [string] $ClientSecret,

  # Identify the gateway & targets
  [Parameter(Mandatory=$true)] [string] $GatewayName,     # friendly name shown in Power BI
  [Parameter(Mandatory=$true)] [string] $WorkspaceId,     # GUID
  [Parameter(Mandatory=$true)] [string] $DatasetId,       # GUID

  # Databricks connection details
  [Parameter(Mandatory=$true)] [string] $DbxServer,       # e.g. adb-<wsid>.<region>.azuredatabricks.net
  [Parameter(Mandatory=$true)] [string] $DbxHttpPath,     # e.g. /sql/1.0/warehouses/<warehouse-id>
  [int] $DbxPort = 443,

  # Choose ONE auth method: PAT or OAuth2
  [string] $DatabricksPAT = $null,        # for PAT (Basic auth: username="token", password=<PAT>)
  [switch] $UseOAuth2CallerIdentity,      # for OAuth2: use the API caller's AAD identity (owner token)
  [switch] $UseOAuth2EndUserSSO,          # for OAuth2: end-user SSO (DirectQuery)
  
  # Optional: grant a user/SP access to the datasource after creation
  [string] $GrantToObjectId = $null,      # Entra objectId of user/service principal to grant 'User'
  [ValidateSet("None","Viewer","User","Admin")] [string] $GrantAccessRight = "User"
)

Import-Module MicrosoftPowerBIMgmt.Profile  -ErrorAction Stop
Import-Module MicrosoftPowerBIMgmt.Data     -ErrorAction Stop

Write-Host "Authenticating to Power BI..."
$secure = ConvertTo-SecureString $ClientSecret -AsPlainText -Force
Connect-PowerBIServiceAccount -ServicePrincipal -Tenant $TenantId -ClientId $ClientId -Credential (New-Object System.Management.Automation.PSCredential($ClientId,$secure))

function Invoke-PBIGet($path)  { Invoke-PowerBIRestMethod -Url "v1.0/myorg/$path" -Method Get    | ConvertFrom-Json }
function Invoke-PBIPost($path,$body) { 
  $json = ($body | ConvertTo-Json -Depth 10)
  Invoke-PowerBIRestMethod -Url "v1.0/myorg/$path" -Method Post -Body $json -ContentType "application/json" | ConvertFrom-Json
}

# ---------- 1) Locate gateway and get its RSA public key ----------
$gateways = Invoke-PBIGet "gateways"   # contains publicKey { modulus, exponent }
$gateway  = $gateways.value | Where-Object { $_.name -eq $GatewayName } | Select-Object -First 1
if (-not $gateway) { throw "Gateway named '$GatewayName' not found." }

# Get full gateway (ensures publicKey present)
$gatewayFull = Invoke-PBIGet "gateways/$($gateway.id)"
$pub = $gatewayFull.publicKey
if (-not $pub.modulus -or -not $pub.exponent) { throw "Gateway public key missing." }

# ---------- helper: encrypt credentials with RSA-OAEP ----------
Add-Type -AssemblyName System.Security
function Encrypt-ForGateway([string]$plaintext,[string]$b64Modulus,[string]$b64Exponent) {
  $rsa = [System.Security.Cryptography.RSA]::Create()
  $params = New-Object System.Security.Cryptography.RSAParameters
  $params.Modulus = [Convert]::FromBase64String($b64Modulus)
  $params.Exponent = [Convert]::FromBase64String($b64Exponent)
  $rsa.ImportParameters($params)
  $bytes = [Text.Encoding]::UTF8.GetBytes($plaintext)
  $cipher = $rsa.Encrypt($bytes,[System.Security.Cryptography.RSAEncryptionPadding]::OaepSHA1)
  [Convert]::ToBase64String($cipher)
}

# ---------- 2) Build connectionDetails for Databricks ----------
$connectionDetails = @{
  server  = $DbxServer          # "Server hostname" in UI
  httpPath= $DbxHttpPath        # "HTTP path"
  port    = $DbxPort
} | ConvertTo-Json

# ---------- 3) Build credentialDetails ----------
if ($DatabricksPAT) {
  # PAT via Basic: username='token', password='<PAT>' (Databricks guidance)
  $credJson = @{ credentialData = @(
      @{ name="username"; value="token" },
      @{ name="password"; value=$DatabricksPAT }
  ) } | ConvertTo-Json -Depth 5
  $encCreds = Encrypt-ForGateway $credJson $pub.modulus $pub.exponent
  $credentialDetails = @{
    credentialType      = "Basic"
    credentials         = $encCreds
    encryptedConnection = "Encrypted"
    encryptionAlgorithm = "RSA-OAEP"
    privacyLevel        = "Organizational"
  }
}
elseif ($UseOAuth2CallerIdentity -or $UseOAuth2EndUserSSO) {
  # OAuth2; choose one SSO mode
  $credJson = @{ credentialData = @() } | ConvertTo-Json  # no secrets; using AAD identity
  $encCreds = Encrypt-ForGateway $credJson $pub.modulus $pub.exponent
  $credentialDetails = @{
    credentialType                 = "OAuth2"
    credentials                    = $encCreds
    encryptedConnection            = "Encrypted"
    encryptionAlgorithm            = "RSA-OAEP"
    privacyLevel                   = "Organizational"
    useCallerAADIdentity           = [bool]$UseOAuth2CallerIdentity.IsPresent
    useEndUserOAuth2Credentials    = [bool]$UseOAuth2EndUserSSO.IsPresent
  }
}
else {
  throw "Select an auth method: provide -DatabricksPAT OR -UseOAuth2CallerIdentity OR -UseOAuth2EndUserSSO."
}

# ---------- 4) Create the Databricks datasource on the gateway ----------
$createBody = @{
  dataSourceType    = "Databricks"
  datasourceName    = "ADB – $($DbxServer)$($DbxHttpPath)"
  connectionDetails = $connectionDetails
  credentialDetails = $credentialDetails
}
Write-Host "Creating Databricks datasource on gateway '$($gateway.name)'..."
$created = Invoke-PBIPost "gateways/$($gateway.id)/datasources" $createBody
$datasourceId = $created.id
Write-Host "Datasource created: $datasourceId"

# ---------- 5) (Optional) Grant another principal access to the datasource ----------
if ($GrantToObjectId) {
  $grantBody = @{
    identifier     = $GrantToObjectId
    principalType  = "App"     # or "User" if granting to a user; adjust as needed
    datasourceAccessRight = $GrantAccessRight
  }
  Invoke-PBIPost "gateways/$($gateway.id)/datasources/$datasourceId/users" $grantBody | Out-Null
  Write-Host "Granted $GrantAccessRight to object $GrantToObjectId on datasource $datasourceId"
}

# ---------- 6) Bind your dataset to the gateway (+ specific datasource) ----------
$bindBody = @{
  gatewayObjectId     = $gateway.id
  datasourceObjectIds = @($datasourceId)
}
Write-Host "Binding dataset $DatasetId in workspace $WorkspaceId to gateway..."
Invoke-PBIPost "groups/$WorkspaceId/datasets/$DatasetId/Default.BindToGateway" $bindBody | Out-Null
Write-Host "Binding complete."
