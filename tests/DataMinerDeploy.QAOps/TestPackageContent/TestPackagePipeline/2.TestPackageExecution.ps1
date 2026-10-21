param (
    [Parameter(Mandatory = $true)]
    [string]$PathToTestPackageContent
)

$ErrorActionPreference = 'Stop'
Install-Module Skyline.DataMiner.QAOps.PipelineLibrary -Repository PSGallery -Scope CurrentUser -Force -MinimumVersion 1.3.0
Import-Module Skyline.DataMiner.QAOps.PipelineLibrary -Force

$env:DATAMINER_DEPLOY_REGRESSION_RESULTS = Join-Path $PathToTestPackageContent 'deployment-results'
$assembly = Join-Path $PathToTestPackageContent 'TestHarvesting\tests.generated\Tests\DataMinerDeploy.qaops.integrationtests.dll'
Invoke-DotNetTestAndPublishResults -PathToTestPackageContent $PathToTestPackageContent -TestDllPath $assembly -ResultsFileName 'deployment-regression.trx'

if (Test-Path -LiteralPath $env:DATAMINER_DEPLOY_REGRESSION_RESULTS) {
    foreach ($file in Get-ChildItem -LiteralPath $env:DATAMINER_DEPLOY_REGRESSION_RESULTS -Recurse -Filter summary.json) {
        $summary = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        Push-TestCaseResult -Outcome OK -Name "DeploymentEvidence_DM_$($summary.DataMinerVersion)_Tool_$($summary.ToolVersion)" `
            -Message ($summary | ConvertTo-Json -Compress) -TestAspect Assertion
    }
}
