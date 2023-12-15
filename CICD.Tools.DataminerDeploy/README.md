# Skyline.DataMiner.CICD.Tools.DataMinerDeploy

## About

Deploys a package to DataMiner from the cloud or directly from a local artifact.

> **Note**
> Deployment from the cloud currently only works for private artifacts. Meaning you need to use an agent key of the same organization (admin.dataminer.services) that was used to perform the upload.

> **Note**
> Usage of this tool is tracked through non-personal metrics provided through a single https call on each use.
>
> These metrics may include, but are not limited to, the frequency of use and the primary purposes for which the Software is employed (e.g., automation, protocol analysis, visualization, etc.). By using the Software, you agree to allow Skyline to collect and analyze such metrics for the purpose of improving and enhancing the Software.

### About DataMiner

DataMiner is a transformational platform that provides vendor-independent control and monitoring of devices and services. Out of the box and by design, it addresses key challenges such as security, complexity, multi-cloud, and much more. It has a pronounced open architecture and powerful capabilities enabling users to evolve easily and continuously.

The foundation of DataMiner is its powerful and versatile data acquisition and control layer. With DataMiner, there are no restrictions to what data users can access. Data sources may reside on premises, in the cloud, or in a hybrid setup.

A unique catalog of 7000+ connectors already exist. In addition, you can leverage DataMiner Development Packages to build you own connectors (also known as "protocols" or "drivers").

> **Note**
> See also: [About DataMiner](https://aka.dataminer.services/about-dataminer).

### About Skyline Communications

At Skyline Communications, we deal in world-class solutions that are deployed by leading companies around the globe. Check out [our proven track record](https://aka.dataminer.services/about-skyline) and see how we make our customers' lives easier by empowering them to take their operations to the next level.

## Getting Started
In commandline:
dotnet tool install -g Skyline.DataMiner.CICD.Tools.DataMinerDeploy

Then run the command
dataminer-package-deploy help

## Deploying from the catalog

Deployment from the cloud can be beneficial when you do not have the local artifact any more (.dmapp, .dmprotocol) or if you have a DataMiner running as a service(DAAS).

If you've uploaded an artifact beforehand you only need to artifact id to deploy it to one or more agents of your organization at any time in the future.

### FromCatalog
The most basic command will allow deployment of an artifact using the artifact identifier returned from performing an upload using ["dataminer-catalog-upload"](https://www.nuget.org/packages/Skyline.DataMiner.CICD.Tools.CatalogUpload).

```console
dataminer-package-deploy from-catalog --artifact-id "dmscript/f764389f-5404-4c32-9ac9-b54366a3d5e0" --dm-catalog-token "cloudConnectedToken"
```

### Authentication and Tokens

You can choose to add the DATAMINER_CATALOG_TOKEN to an environment variable instead and skip having to pass along the secure token.
```console
dataminer-package-deploy from-catalog --artifact-id "dmscript/f764389f-5404-4c32-9ac9-b54366a3d5e0"
```
 
 There are 2 options to store the key in an environment variable:
- key stored as an Environment Variable called "DATAMINER_CATALOG_TOKEN". (unix/win)
- key configured one-time using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys called "DATAMINER_CATALOG_TOKEN_ENCRYPTED" (windows only)

The first option is commonplace for environment setups in cloud-based CI/CD Pipelines (github, gitlab, azure, ...)
The second option can be beneficial on a static server such as Jenkins or your local machine (windows only). It adds additional encryption to the environment variable only allowing decryption on the same machine. 

Running as Administrator:
```console
dotnet tool install -g Skyline.DataMiner.CICD.Tools.WinEncryptedKeys
WinEncryptedKeys --name "DATAMINER_CATALOG_TOKEN_ENCRYPTED" --value "MyTokenHere"
```

> **Note**
> Make sure you close your commandline tool so it clears the history.
> This only works on windows machines.

You can review and make suggestions to the sourcecode of this encryption tool here: 
https://github.com/SkylineCommunications/Skyline.DataMiner.CICD.Tools.WinEncryptedKeys


## Deploying from a local artifact

Deployment from a local artifact directly to a self-hosted DataMiner is also possible. 

This can be useful when there are self-hosted static staging and production systems on a local network that are not internet accessible.

 ### FromArtifact

 The most basic command will allow deployment of an artifact using the path to the artifact and a local DataMiner user name and password.

```console
dataminer-package-deploy from-artifact --path-to-artifact "" --dm-server-location "" --dm-user "" --dm-password ""
```
