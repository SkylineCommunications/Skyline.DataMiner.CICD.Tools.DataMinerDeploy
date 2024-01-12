namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    using Microsoft.Extensions.Logging;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerArtifacts;
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService;

    /// <summary>
    /// Creates an instance of <see cref="IArtifact"></see> that can be used for deployment to a DataMiner.
    /// </summary>
    public static class DeploymentFactory
    {
        /// <summary>
        /// A deployment using a catalog artifact identifier from the cloud, to a cloud connected agent.
        /// </summary>
        /// <remarks>WARNING: when wishing to deploy several Artifacts it's recommended to use the IArtifact Cloud(ICatalogService service, string artifactIdentifier, string catalogAgentToken, ILogger logger).</remarks>
        /// <param name="artifactIdentifier">The unique cloud artifact identifier as returned from performing a catalog-upload.</param>
        /// <param name="catalogAgentToken">A provided token for the agent as defined in https://admin.dataminer.services/.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> that will hold error, debug and other information.</param>
        /// <returns>An instance of <see cref="IArtifact"/> that allows deployment.</returns>
        public static IArtifact Cloud(string artifactIdentifier, string catalogAgentToken, ILogger logger)
        {
            logger.LogDebug($"Attempting Deployment with provided argument as token for artifact: {artifactIdentifier}...");
            return new CatalogArtifact(artifactIdentifier, catalogAgentToken, logger);
        }

        /// <summary>
        /// A deployment using a catalog artifact identifier from the cloud, to a cloud connected agent.
        /// </summary>
        /// <param name="service">An instance of <see cref="ICatalogService"/> used for communication with the catalog.</param>
        /// <param name="artifactIdentifier">The unique cloud artifact identifier as returned from performing a catalog-upload.</param>
        /// <param name="catalogAgentToken">A provided token for the agent as defined in https://admin.dataminer.services/.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> that will hold error, debug and other information.</param>
        /// <returns>An instance of <see cref="IArtifact"/> that allows deployment.</returns>
        public static IArtifact Cloud(ICatalogService service, string artifactIdentifier, string catalogAgentToken, ILogger logger)
        {
            return new CatalogArtifact(service, artifactIdentifier, catalogAgentToken, logger);
        }

        /// <summary>
        /// A deployment using a catalog artifact identifier from the cloud, to a cloud connected agent. This uses the DATAMINER_CATALOG_TOKEN or DATAMINER_CATALOG_TOKEN environment variable as the token for the agent.
        /// </summary>
        /// <remarks>WARNING: when wishing to deploy several Artifacts it's recommended to use the IArtifact Cloud(ICatalogService service, string artifactIdentifier, string catalogAgentToken, ILogger logger).</remarks>
        /// <param name="artifactIdentifier">The unique cloud artifact identifier as returned from performing a catalog-upload.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> that will hold error, debug and other information.</param>
        /// <returns>An instance of <see cref="IArtifact"/> that allows deployment.</returns>
        public static IArtifact Cloud(string artifactIdentifier, ILogger logger)
        {
            return new CatalogArtifact(artifactIdentifier, logger);
        }

        /// <summary>
        /// A deployment using a catalog artifact identifier from the cloud, to a cloud connected agent. This uses the DATAMINER_CATALOG_TOKEN or DATAMINER_CATALOG_TOKEN environment variable as the token for the agent.
        /// </summary>
        /// <param name="service">An instance of <see cref="ICatalogService"/> used for communication with the catalog.</param>
        /// <param name="artifactIdentifier">The unique cloud artifact identifier as returned from performing a catalog-upload.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> that will hold error, debug and other information.</param>
        /// <returns>An instance of <see cref="IArtifact"/> that allows deployment.</returns>
        public static IArtifact Cloud(ICatalogService service, string artifactIdentifier, ILogger logger)
        {
            return new CatalogArtifact(service, artifactIdentifier, logger);
        }

        /// <summary>
        /// A deployment using a local package to a local network connected DataMiner agent.
        /// </summary>
        /// <remarks>IMPORTANT: Deployment of a legacy style Application package (.dmapp that contains a Update.Zip inside) will restart the agent.</remarks>
        /// <param name="fs">An instance of <see cref="IFileSystem"/> used to access directories and files.</param>
        /// <param name="pathToArtifact">Path to the application package (.dmapp) or protocol package (.dmprotocol).</param>
        /// <param name="dataMinerServerLocation">The IP or host name of a DataMiner agent.</param>
        /// <param name="dataMinerUser">The DataMiner User to setup a direct connection to an accessible agent.</param>
        /// <param name="dataMinerPassword">The password to setup a direct connection to an accessible agent.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> that will hold error, debug and other information.</param>
        /// <returns>An instance of <see cref="IArtifact"/> that allows deployment.</returns>
        public static IArtifact Local(IFileSystem fs, string pathToArtifact, ILogger logger, string dataMinerServerLocation, string dataMinerUser = "", string dataMinerPassword = "")
        {
            return new LocalArtifact(pathToArtifact, dataMinerServerLocation, dataMinerUser, dataMinerPassword, logger, fs);
        }

        /// <summary>
        /// A deployment using a local package to a local network connected DataMiner agent.
        /// </summary>
        /// <remarks>IMPORTANT: Deployment of a legacy style Application package (.dmapp that contains a Update.Zip inside) will restart the agent.</remarks>
        /// <param name="fs">An instance of <see cref="IFileSystem"/> to access directories and files.</param>
        /// <param name="dmService">An instance of <see cref="IDataMinerService"/> used for direct communication with a DataMiner.</param>
        /// <param name="pathToArtifact">Path to the application package (.dmapp) or protocol package (.dmprotocol).</param>
        /// <param name="dataMinerServerLocation">The IP or host name of a DataMiner agent.</param>
        /// <param name="dataMinerUser">The DataMiner User to setup a direct connection to an accessible agent.</param>
        /// <param name="dataMinerPassword">The password to setup a direct connection to an accessible agent.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> that will hold error, debug and other information.</param>
        /// <returns>An instance of <see cref="IArtifact"/> that allows deployment.</returns>
        public static IArtifact Local(IFileSystem fs, IDataMinerService dmService, string pathToArtifact, ILogger logger, string dataMinerServerLocation, string dataMinerUser = "", string dataMinerPassword = "")
        {
            return new LocalArtifact(dmService, pathToArtifact, dataMinerServerLocation, dataMinerUser, dataMinerPassword, logger, fs);
        }
    }
}