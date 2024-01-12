namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    /// <summary>
    /// Instances of <see cref="DeployedPackage"/> represent a package that was deployed.
    /// </summary>
    public class DeployedPackage
    {
        /// <summary>
        /// Creates an instance of <see cref="DeployedPackage"/>.
        /// </summary>
        /// <param name="status">Status of the deployment.</param>
        public DeployedPackage(string status)
        {
            Status = status;
        }

        /// <summary>
        /// The status of the deployment.
        /// succeeded, error, timeout
        /// </summary>
        public string Status { get; }
    }
}