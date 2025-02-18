namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.CatalogService
{
    /// <summary>
    /// Represents a unique identifier for a deployment in the DataMiner Catalog.
    /// </summary>
    public class KeyCatalogDeploymentIdentifier
    {
        /// <summary>
        /// Gets or sets the globally unique identifier (GUID) of the catalog entry.
        /// </summary>
        public string CatalogGuid { get; set; }

        /// <summary>
        /// Gets or sets the version of the catalog entry.
        /// </summary>
        public string CatalogVersion { get; set; }

        /// <summary>
        /// Gets or sets the globally unique identifier (GUID) of the deployment destination.
        /// </summary>
        public string DestinationGuid { get; set; }
    }
}
