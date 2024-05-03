namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Service interface used to actually upload and artifact.
    /// </summary>
    public interface ICatalogService
    {
        /// <summary>
        /// Starts deployment of an artifact from an external store, to a cloud-connected Agent. Use <see cref="GetDeployedPackageAsync"/> to check if it is finished.
        /// </summary>
        /// <param name="artifactIdentifier">The unique cloud artifact identifier as returned from performing a catalog-upload.</param>
        /// <param name="key">The key to deploy to a specific cloud-connected DataMiner as defined in admin.dataminer.services.</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/> to cancel an ongoing deploy.</param>
        /// <returns>An instance of <see cref="DeployingPackage"/>.</returns>
        Task<DeployingPackage> DeployPackageAsync(string artifactIdentifier, string key, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of a deploying/deployed package. Can be wait to verify success, failure or timeout.
        /// </summary>
        /// <param name="deployingPackage">An instance of <see cref="DeployingPackage"/> to verify status of an ongoing deployment.</param>
        /// <param name="key">The key to deploy to a specific cloud-connected DataMiner as defined in admin.dataminer.services.</param>
        /// <returns>An instance of <see cref="DeployedPackage"/>.</returns>
        Task<DeployedPackage> GetDeployedPackageAsync(DeployingPackage deployingPackage, string key);
    }
}