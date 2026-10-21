# DataMinerDeploy.RegressionFixture

**Project Type**: Automation script

**Size**: XS

## Summary

Produces the harmless **modern `.dmapp` fixture** deployed by the DataMinerDeploy QAOps regression. Its script contains a versioned marker that lets the test verify the installed payload instead of trusting the CLI exit code. The DataMiner SDK builds and packages the fixture.

## Purpose

This is an internal regression fixture, not an operator workflow. The deployment test verifies its installed XML; it does not execute the script.

## Interactions

The script only logs `DataMinerDeploy regression fixture 1.0.0` if executed. It does not change elements, services, views, alarms, profiles, or other DataMiner objects.

## Input Parameters

This script has no input parameters.

## Build and compatibility

The [QAOps test package](../DataMinerDeploy.QAOps/README.md) builds this project and bundles the resulting `DataMinerDeploy.RegressionFixture.1.0.0.dmapp` as test input. It must not be added to the package's automatically installed project references, because that would invalidate the deployment assertion.

The fixture declares a minimum DataMiner version of `10.4.0.0 - 14003`. This does not alter the deployment tool's own minimum supported version.
