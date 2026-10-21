# DataMinerDeploy.IntegrationTests

**Project Type**: Test project

**Size**: S

## Summary

Hosts the **QAOps-only MSTest regression** that launches Windows PowerShell to deploy a bundled `.dmapp` using the published DataMinerDeploy tool. It captures redacted output, attaches diagnostics to TRX, and fails on deployment errors, timeout, or a missing verified result. Credentials are retrieved from QAOps encrypted keys and passed only through the child process environment.

## Execution

This .NET Framework 4.8/x86 test runs inside the [QAOps test package](../DataMinerDeploy.QAOps/README.md), not on a developer machine. The child deployment tool uses x64 .NET 10.

`DeploymentRegression.psd1` pins the tool version, exact NuGet SHA-256, fixture version, and expected script marker. `Invoke-DeploymentRegression.ps1` performs the deployment, assertions, diagnostic collection, and test-owned script cleanup.

See the test package README for the build command, release matrix configurations, result inspection, and coverage limits.
