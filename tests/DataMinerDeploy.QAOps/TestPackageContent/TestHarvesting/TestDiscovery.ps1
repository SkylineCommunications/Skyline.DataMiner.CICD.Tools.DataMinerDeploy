$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$source = Join-Path $PSScriptRoot 'postbuild.generated'
$destination = Join-Path $PSScriptRoot 'tests.generated'
$config = Import-PowerShellDataFile (Join-Path $source 'Tests\DeploymentRegression.psd1')
if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force

$feed = Join-Path $destination 'ToolFeed'
New-Item -ItemType Directory -Path $feed -Force | Out-Null
$id = 'skyline.dataminer.cicd.tools.dataminerdeploy'
$version = $config.ToolVersion
$package = Join-Path $feed "$id.$version.nupkg"
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $package -UseBasicParsing
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $config.ToolPackageSha256) {
    throw "Published DataMinerDeploy $version does not match the pinned SHA-256."
}
Write-Host "Harvested deployment regression, fixture, and DataMinerDeploy $version."
exit 0
