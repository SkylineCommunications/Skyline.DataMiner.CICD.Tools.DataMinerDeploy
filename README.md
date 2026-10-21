# Skyline.DataMiner.CICD.Tools.DataMinerDeploy

## About

Deploys a package to DataMiner from the catalog or directly.

## Projects

### Deployment tool

| Project | Description |
|---------|-------------|
| [CICD.Tools.DataMinerDeploy](CICD.Tools.DataMinerDeploy/README.md) | Command-line deployment tool. |
| [CICD.Tools.DataMinerDeploy.Lib](Skyline.DataMiner.CICD.Tools.DataminerDeploy.Lib/README.md) | Deployment library. |
| [CICD.Tools.DataMinerDeployTests](CICD.Tools.DataMinerDeployTests/CICD.Tools.DataMinerDeployTests.csproj) | CLI unit tests. |
| [CICD.Tools.DataMinerDeploy.LibTests](CICD.Tools.DataMinerDeploy.LibTests/CICD.Tools.DataMinerDeploy.LibTests.csproj) | Library unit tests. |

### Real DataMiner regression

| Project | Description |
|---------|-------------|
| [DataMinerDeploy.QAOps](tests/DataMinerDeploy.QAOps/README.md) | Repeatable PowerShell deployment regression against Main, Feature, and RC using a pinned published tool package. |
| [DataMinerDeploy.IntegrationTests](tests/DataMinerDeploy.IntegrationTests/README.md) | QAOps-only MSTest wrapper and deployment assertions. |
| [DataMinerDeploy.RegressionFixture](tests/DataMinerDeploy.RegressionFixture/README.md) | Harmless script `.dmapp` used to verify actual installation. |
