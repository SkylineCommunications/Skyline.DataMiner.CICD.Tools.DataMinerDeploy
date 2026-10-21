# DataMinerDeploy.QAOps

**Project Type**: QAOps test package

**Size**: S

## Summary

Builds a repeatable **DataMinerDeploy .NET 10 deployment regression** package for QAOps. It bundles a harmless script `.dmapp`, an MSTest wrapper, PowerShell, and the exact published **4.0.0-alpha1** tool package. The fixture is deliberately not installed by the test package: the deployment CLI must install it during the test.

## Build

From the repository root on Windows, with .NET 10 SDK installed:

```powershell
dotnet build .\tests\DataMinerDeploy.QAOps\DataMinerDeploy.QAOps.csproj --configuration Release
```

The SDK produces `bin\Release\net48\DataMinerBuild\DataMinerDeploy.QAOps.1.0.3.dmtest` under this project. The build also builds the integration tests and fixture, downloads the published deployment tool from NuGet.org, and verifies its SHA-256. No Catalog download key is needed because there are no external Catalog dependencies.

## Run on QAOps

Install the `Skyline.DataMiner.QAOps` .NET tool if needed. Supply a short-lived QAOps token through `QAOPS_TOKEN`; never commit a token. The target receives its DataMiner credentials through the encrypted `QAOpsDataMinerUser` and `QAOpsDataMinerPassword` keys provided by QAOps.

```powershell
$package = Resolve-Path .\tests\DataMinerDeploy.QAOps\bin\Release\net48\DataMinerBuild\DataMinerDeploy.QAOps.1.0.3.dmtest
dataminer-qaops test-run-and-wait `
    -t 01KTRNHRHWQJ9SMESYJAFP8SE9 `
    -c 01KTRNTY3WW3JANCQ7WPWBEAVM `
    -san saqaops `
    --token "$env:QAOPS_TOKEN" `
    --test-packages "$package" `
    -tags "Deploy400alpha1-Main-$(Get-Date -Format yyyyMMddHHmmss)" `
    -rf "$env:TEMP\DataMinerDeploy-Main.json"
```

Repeat with the required configuration. Let every submitted run finish; stopping the local CLI does not cancel the remote run.

Use `-san saqaopsstaging` only when staging is required and the token has access to that storage account. A storage upload authorization failure occurs before a DataMiner run is created.

| Target | Configuration |
|--------|---------------|
| Latest Main | `01KTRNTY3WW3JANCQ7WPWBEAVM` |
| Latest Feature | `01KTRNSARVCH5DG50QM0RW7GYA` |
| Latest RC | `01KTRNMVWEZX5EAW3GRDB744G4` |

Use distinct tags and result filenames for each run. Require the named `PowerShell_FromArtifact_InstallsBundledDmappOnNet10` test to pass, not merely a successful pipeline status. The result JSON contains `BUILD_URL` for the exact QAOps run.

## Assertions and diagnostics

The test installs the bundled tool into a private directory, installing an x64 .NET 10 SDK there if necessary. It invokes `from-artifact` against localhost with debug output and a 600-second deployment timeout. Success requires the expected installed script name and payload marker, plus running SLDataMiner, SLNet, SLDMS, and SLWatchDog services.

The test refuses a pre-existing fixture and removes the script it successfully identifies after deployment. Use clean, disposable QAOps targets: failed partial installations and application installation records are not fully rolled back.

`deployment-results` on the target contains redacted PowerShell output, deployment output, a verified `summary.json`, SLAutomation/SLNet log tails, and any newly modified upgrade progress logs found. Top-level diagnostic files are attached to the TRX. Failure assertion messages also include the captured output so the QAOps result is actionable without downloading attachments. An additional `DeploymentEvidence_DM_..._Tool_...` result records the actual Agent and tool versions.

## Coverage and maintenance

This tests a modern AppInfo-based `.dmapp`: authentication, SLNet serialization, upload, installation, and the resulting script payload. It does **not** test a legacy Update.zip-based application package, a full `.dmupgrade`, or restart/UpgradeService behavior. The generated fixture and test package require DataMiner 10.4.0 or later; that is a test-fixture requirement, not a change to the deployment tool's minimum supported Agent version.

To test another published tool version, update `ToolVersion` and `ToolPackageSha256` in `..\DataMinerDeploy.IntegrationTests\DeploymentRegression.psd1`. Increment this project's package `Version` whenever test content changes, rebuild, and verify the expected test is discoverable from the assembly extracted from that exact versioned `.dmtest` before submitting it.

The integration project is named `*.qaops.integrationtests.csproj` to distinguish it from local unit tests. Do not execute it with local `dotnet test`: it requires a real QAOps DataMiner and the target's encrypted credentials.