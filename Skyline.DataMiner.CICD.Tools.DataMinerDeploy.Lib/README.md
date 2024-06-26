# Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib

## About

Library code containing ways to deploy an artifact to DataMiner.

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

<!-- Uncomment below and add more info to provide more information about how to use this package. -->
<!-- ## Getting Started -->
