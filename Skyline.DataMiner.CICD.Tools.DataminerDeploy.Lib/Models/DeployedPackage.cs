namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    /// <summary>
    /// Instances of <see cref="DeployedPackage"/> represent a package that was deployed.
    /// </summary>
    public class DeployedPackage
    {
        /// <summary>
        /// Creates an instance of the <see cref="DeployedPackage"/> class.
        /// </summary>
        /// <param name="status">Status of the deployment.</param>
        public DeployedPackage(string status)
        {
            Status = status;
        }

        /// <summary>
        /// Gets the status of the deployment.
        /// </summary>
        /// <value>The status of the deployment: succeeded, error or timeout.</value>
        public string Status { get; }
    }
}