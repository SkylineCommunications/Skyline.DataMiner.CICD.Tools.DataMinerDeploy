# Skyline.DataMiner.CICD.Tools.DataMinerDeploy

## About

Deploys a package to DataMiner from the cloud or directly from a local artifact.

> **Note**
> Usage of this tool is tracked through non-personal metrics provided through a single https call on each use.
>
> These metrics may include, but are not limited to, the frequency of use and the primary purposes for which the Software is employed (e.g., automation, protocol analysis, visualization, etc.). By using the Software, you agree to allow Skyline to collect and analyze such metrics for the purpose of improving and enhancing the Software.


### Limitations

The deployment tool attempts to work with as many scenarios possible.
There are currently however a couple of limitations.

#### Local Artifacts

- Deployment with a local artifact requires DataMiner 10.3.0/10.3.2 or higher.

- Deployment with a local artifact only works from windows machines.

- Deployment with a local artifact of type: legacy style application package, to your localhost is currently not supported.


#### Catalog

- Deployment from the cloud currently only works for private artifacts. Meaning you need to use an agent key of the same organization (admin.dataminer.services) that was used to perform the upload.


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

Deployment from the cloud can be beneficial when you do not have the local artifact any more (.dmapp, .dmprotocol). It is also required if you have a DataMiner running as a service (DaaS).

If you've uploaded an artifact beforehand you only need to artifact id to deploy it to one or more agents of your organization at any time in the future.

### Basic Command
The most basic command will allow deployment of an artifact using the artifact identifier returned from performing an upload using ["dataminer-catalog-upload"](https://www.nuget.org/packages/Skyline.DataMiner.CICD.Tools.CatalogUpload).

```console
dataminer-package-deploy from-catalog --artifact-id "dmscript/f764389f-5404-4c32-9ac9-b54366a3d5e0" --dm-catalog-token "cloudConnectedToken"
```

### Authentication and Tokens

You can choose to add the DATAMINER_CATALOG_TOKEN to an environment variable instead and skip having to pass along the dm-catalog-token.
```console
dataminer-package-deploy from-catalog --artifact-id "dmscript/f764389f-5404-4c32-9ac9-b54366a3d5e0"
```
 
 There are 2 options to store the key in an environment variable:
- key stored as an environment variable called "DATAMINER_CATALOG_TOKEN". (Unix/Windows)
- key configured one-time using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys called "DATAMINER_CATALOG_TOKEN_ENCRYPTED" (Windows only)

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

You can deploy 3 types of packages:

- Connector or Protocol Packages (.dmprotocol)
- DataMiner Application Packages (.dmapp)
- Legacy DataMiner Application Packages (.dmapp that when unzipped, contains an Update.zip file)

> **Important**
> When installing a legacy DataMiner Application Package the agent will restart. The other 2 options do not restart the agent.

> **Note**
> Usage of the Skyline.DataMiner.CICD.Tools.Packager tool or downloading from the catalog will not result in Legacy packages.
> Legacy packages come from deprecated Package Creation software or manual creation.

> **Note**
> Deployment with a local artifact requires DataMiner 10.3.0/10.3.2 or higher.


 ### Basic Command

 The most basic command will allow deployment of an artifact using the path to the artifact and a local DataMiner user name and password.

```console
dataminer-package-deploy from-artifact --path-to-artifact "" --dm-server-location "" --dm-user "" --dm-password ""
```

### Authentication and Tokens

You can choose to add the DATAMINER_DEPLOY_USER and the DATAMINER_DEPLOY_PASSWORD to environment variables instead and skip having to pass along the dm-user and dm-password variables every time.

```console
dataminer-package-deploy from-artifact --path-to-artifact "" --dm-server-location ""
```
 
 There are 2 options to store the key in an environment variable:
- key stored as an Environment Variable called "DATAMINER_DEPLOY_USER" and DATAMINER_DEPLOY_PASSWORD. (Unix/Windows)
- key configured one-time using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys called "DATAMINER_DEPLOY_USER_ENCRYPTED" and "DATAMINER_DEPLOY_PASSWORD_ENCRYPTED" (Windows only)

The first option is commonplace for environment setups in cloud-based CI/CD Pipelines (GitHub Actions, GitLab pipelines, Azure pipelines, ...)
The second option can be beneficial on a static server such as Jenkins or your local machine (windows only). It adds additional encryption to the environment variable only allowing decryption on the same machine. 

Running as Administrator:
```console
dotnet tool install -g Skyline.DataMiner.CICD.Tools.WinEncryptedKeys
WinEncryptedKeys --name "DATAMINER_DEPLOY_USER_ENCRYPTED" --value "MyDmaUsername"
WinEncryptedKeys --name "DATAMINER_DEPLOY_PASSWORD_ENCRYPTED" --value "MyPassword"
```

> **Note**
> Make sure you close your commandline tool so it clears the history.
> This only works on windows machines.

You can review and make suggestions to the sourcecode of this encryption tool here: 
https://github.com/SkylineCommunications/Skyline.DataMiner.CICD.Tools.WinEncryptedKeys

### Post Actions

It's possible to define a '--post-action' argument, this is an action to be executed after initial deployment.

Currently the following options are possible:

 - SetToProduction:


This will only work for protocol packages (.dmprotocol). After deployment, it will set the deployed version as production.


 - SetToProductionIncludingTemplates:

This will only work for protocol packages (.dmprotocol). After deployment, it will set the deployed version as protocol and also copy over all provided templates into production.


For example

```console
dataminer-package-deploy from-artifact --path-to-artifact "" --dm-server-location "" --post-action SetToProduction
```