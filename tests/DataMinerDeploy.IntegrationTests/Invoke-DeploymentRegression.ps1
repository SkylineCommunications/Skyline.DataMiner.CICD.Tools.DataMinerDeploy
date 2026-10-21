param (
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-Host "PowerShell $($PSVersionTable.PSVersion); 64-bit process: $([Environment]::Is64BitProcess)"
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'DeploymentRegression.psd1')
$inputs = Split-Path $PSScriptRoot -Parent
$dmRoot = 'C:\Skyline DataMiner'
$started = Get-Date
$installed = @()
$failure = $null

function Protect-Output([string]$Text) {
    foreach ($secret in @($env:DATAMINER_DEPLOY_USER, $env:DATAMINER_DEPLOY_PASSWORD)) {
        if (-not [string]::IsNullOrEmpty($secret)) {
            $Text = $Text.Replace($secret, '[REDACTED]')
        }
    }
    return $Text
}

function Invoke-Native([string]$File, [string[]]$Arguments, [int]$Timeout = 900) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $File
    $info.Arguments = ($Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.CreateNoWindow = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($Timeout * 1000)) {
            $process.Kill()
            throw "Process '$File' exceeded $Timeout seconds."
        }
        $text = Protect-Output ($stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult())
        Write-Host $text
        if ($process.ExitCode -ne 0) {
            throw "Process '$File' exited $($process.ExitCode): $text"
        }
        return $text
    }
    finally {
        $process.Dispose()
    }
}

function Get-FixtureScripts {
    @(Get-ChildItem -LiteralPath (Join-Path $dmRoot 'Scripts') -Recurse -File -Filter "*$($config.FixtureName)*.xml" |
        Where-Object {
            [xml]$xml = Get-Content -LiteralPath $_.FullName -Raw
            $xml.SelectSingleNode('/*[local-name()="DMSScript"]/*[local-name()="Name"]').InnerText -eq $config.FixtureName
        })
}

function Save-LogTail([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Write-Host "No diagnostic log at '$Path'."
        return
    }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        [void]$stream.Seek([Math]::Max(0, $stream.Length - 65536), [IO.SeekOrigin]::Begin)
        $reader = New-Object IO.StreamReader($stream)
        try {
            $text = Protect-Output (($reader.ReadToEnd() -split '\r?\n' | Select-Object -Last 80) -join [Environment]::NewLine)
            Set-Content -LiteralPath (Join-Path $ResultsDirectory $Name) -Value $text -Encoding UTF8
            Write-Host "--- $Path ---`n$text"
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

try {
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    $version = (Get-Item -LiteralPath (Join-Path $dmRoot 'Files\SLDataMiner.exe')).VersionInfo.ProductVersion
    Write-Host "DataMiner version: $version; machine: $env:COMPUTERNAME; started: $($started.ToString('o'))"
    Write-Host "Core services before deployment:"
    Get-Service -Name SLDataMiner, SLNet, SLDMS, SLWatchDog | Format-Table Name, Status | Out-String | Write-Host
    $existing = @(Get-FixtureScripts)
    if ($existing.Count -ne 0) {
        throw "Fixture already present; refusing to accept a stale installation or overwrite it: $($existing.FullName -join ', ')"
    }

    $dotnet = Join-Path $env:ProgramW6432 'dotnet\dotnet.exe'
    $sdks = Invoke-Native $dotnet @('--list-sdks')
    if ($sdks -notmatch '(?m)^10\.') {
        $installer = Join-Path $ResultsDirectory 'dotnet-install.ps1'
        Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
        $sdkRoot = Join-Path $ResultsDirectory 'dotnet'
        & $installer -Channel '10.0' -Architecture x64 -InstallDir $sdkRoot -NoPath
        $dotnet = Join-Path $sdkRoot 'dotnet.exe'
        if (-not (Test-Path -LiteralPath $dotnet)) {
            throw '.NET 10 SDK installation did not produce dotnet.exe.'
        }
    }
    $env:DOTNET_ROOT = Split-Path $dotnet -Parent
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $feed = Join-Path $inputs 'ToolFeed'
    $nupkg = Join-Path $feed "skyline.dataminer.cicd.tools.dataminerdeploy.$($config.ToolVersion).nupkg"
    $packageHash = (Get-FileHash -LiteralPath $nupkg -Algorithm SHA256).Hash
    if ($packageHash -ne $config.ToolPackageSha256) {
        throw 'Bundled DataMinerDeploy NuGet package failed its SHA-256 check.'
    }
    $nugetConfig = Join-Path $ResultsDirectory 'NuGet.Config'
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    Set-Content -LiteralPath $nugetConfig -Value "<configuration><packageSources><clear/><add key=`"BundledTool`" value=`"$escapedFeed`"/></packageSources></configuration>" -Encoding UTF8
    $toolPath = Join-Path $ResultsDirectory 'tool'
    $null = Invoke-Native $dotnet @('tool', 'install', 'Skyline.DataMiner.CICD.Tools.DataMinerDeploy',
        '--version', $config.ToolVersion, '--tool-path', $toolPath, '--configfile', $nugetConfig, '--no-cache')
    $tool = Join-Path $toolPath 'dataminer-package-deploy.exe'
    $toolVersion = Invoke-Native $tool @('--version')
    if ($toolVersion -notmatch [regex]::Escape($config.ToolVersion)) {
        throw "Installed deployment tool is not version $($config.ToolVersion): $toolVersion"
    }

    $artifact = Join-Path $inputs "Fixtures\$($config.FixtureName).$($config.FixtureVersion).dmapp"
    $artifactHash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
    Write-Host "Deploying $artifact; SHA-256 $artifactHash; tool $($config.ToolVersion); package SHA-256 $packageHash"
    $deploymentOutput = Invoke-Native $tool @('from-artifact', '--path-to-artifact', $artifact,
        '--dm-server-location', 'localhost', '--deploy-timeout-in-seconds', '600', '--debug')
    Set-Content -LiteralPath (Join-Path $ResultsDirectory 'deployment.log') -Value $deploymentOutput -Encoding UTF8
    $deadline = (Get-Date).AddMinutes(2)
    do {
        $installed = @(Get-FixtureScripts)
        if ($installed.Count -eq 1) { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    if ($installed.Count -ne 1) {
        throw "Expected one installed '$($config.FixtureName)' script, found $($installed.Count)."
    }
    $installedXml = Get-Content -LiteralPath $installed[0].FullName -Raw
    if (-not $installedXml.Contains($config.FixtureMarker)) {
        throw "Installed script '$($installed[0].FullName)' does not contain the expected fixture payload."
    }
    $services = @(Get-Service -Name SLDataMiner, SLNet, SLDMS, SLWatchDog)
    if (@($services | Where-Object Status -ne Running).Count -ne 0) {
        throw "Core services are not healthy: $(($services | Format-Table Name, Status | Out-String))"
    }
    $summary = [ordered]@{
        DataMinerVersion = $version
        Machine = $env:COMPUTERNAME
        ToolVersion = $config.ToolVersion
        ToolPackageSha256 = $packageHash
        ArtifactSha256 = $artifactHash
        InstalledScript = $installed[0].FullName
        PayloadVerified = $true
        CoreServicesRunning = $true
        Started = $started.ToString('o')
        Finished = (Get-Date).ToString('o')
    }
    $json = $summary | ConvertTo-Json
    Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json') -Value $json -Encoding UTF8
    Write-Host "VERIFIED DEPLOYMENT: $json"
}
catch {
    $failure = Protect-Output $_.Exception.ToString()
    Write-Host "DEPLOYMENT FAILURE: $failure"
}
finally {
    try {
        Save-LogTail (Join-Path $dmRoot 'Logging\SLAutomation.txt') 'SLAutomation-tail.log'
        Save-LogTail (Join-Path $dmRoot 'Logging\SLNet.txt') 'SLNet-tail.log'
        $upgrades = Join-Path $dmRoot 'Upgrades\Packages'
        if (Test-Path -LiteralPath $upgrades) {
            foreach ($progress in Get-ChildItem -LiteralPath $upgrades -Filter progress.log -Recurse -File |
                Where-Object LastWriteTime -ge $started | Select-Object -First 5) {
                Save-LogTail $progress.FullName ("upgrade-$($progress.Directory.Name).log")
            }
        }
        foreach ($script in $installed) {
            Remove-Item -LiteralPath $script.FullName -Force
            Write-Host "Removed test-owned script '$($script.FullName)'."
        }
    }
    catch {
        Write-Host "DIAGNOSTIC/CLEANUP FAILURE: $(Protect-Output $_.Exception.ToString())"
        if ($null -eq $failure) { $failure = 'Deployment diagnostics or fixture cleanup failed.' }
    }
}
if ($null -ne $failure) {
    exit 1
}
