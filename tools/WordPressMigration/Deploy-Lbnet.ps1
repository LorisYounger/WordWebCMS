param(
    [Parameter(Mandatory=$true)][string]$PrivateConfig,
    [Parameter(Mandatory=$true)][string]$PublishPath,
    [Parameter(Mandatory=$true)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath $PrivateConfig -Raw | ConvertFrom-Json
$destinationRoot = [IO.Path]::GetFullPath($Destination)
if ($destinationRoot -ne 'F:\Downloads\lbcp.exlb.net\202608111453\wwcms_lbnet') { throw 'Unexpected deployment destination.' }
if (Test-Path -LiteralPath $destinationRoot) { throw 'Destination exists; refusing to overwrite it.' }
if (!(Test-Path -LiteralPath (Join-Path $PublishPath 'WordWebCMS.dll'))) { throw 'Missing published application.' }
$verification = Get-Content -LiteralPath (Join-Path $config.OutputPath 'verification.json') -Raw | ConvertFrom-Json
if (!$verification.Passed) { throw 'Migration verification has not passed.' }
New-Item -ItemType Directory -Path $destinationRoot | Out-Null
Get-ChildItem -LiteralPath $PublishPath -Force | Copy-Item -Destination $destinationRoot -Recurse
function New-PrivateDirectory([string]$path) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $identities = @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'), [Security.Principal.SecurityIdentifier]::new('S-1-5-18'))
    foreach ($identity in $identities) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -LiteralPath $path -AclObject $acl
}
$data = Join-Path $destinationRoot 'App_Data'
$private = Join-Path $destinationRoot 'Migration-private'
New-PrivateDirectory $data
New-PrivateDirectory $private
Get-ChildItem -LiteralPath (Join-Path $config.OutputPath 'data') | Copy-Item -Destination $data -Recurse
@{ConnectionStrings=@{Accounts=$config.TargetConnection;Site=$config.TargetConnection}} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $data 'setup.json')
@{Storage=@{Path='App_Data'};Site=@{CookieName='WordWebCMS-lbnet'};Urls='http://127.0.0.1:5197';AllowedHosts='127.0.0.1;localhost;www.exlb.net'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destinationRoot 'appsettings.Production.json')
foreach ($name in @('source-snapshot.json','inspection.json','content-manifest.json','media-manifest.json','pending-category-relations.json','legacy-url-map.json','admin-credentials.private.json','import-map.json','verification.json','mail-profile.json')) {
    Copy-Item -LiteralPath (Join-Path $config.OutputPath $name) -Destination $private
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-Preview.ps1'),(Join-Path $PSScriptRoot 'Stop-Preview.ps1') -Destination $destinationRoot
Get-ChildItem -LiteralPath $PublishPath -Recurse -File | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($PublishPath), $_.FullName)
    if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destinationRoot $relative)).Hash) { throw "Deployment hash mismatch: $relative" }
}
Write-Output 'Published application copied and all deployment file hashes verified.'
