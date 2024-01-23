namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an artifact you wish to deploy to a DataMiner system.
    /// </summary>
    public interface IArtifact : IDisposable
    {
        /// <summary>
        /// Adds a series of actions to attempt after deployment.
        /// </summary>
        /// <param name="postDeployActions">An instance of <see cref="PostDeployActions"/> indicating what actions to try performing after deployment.</param>
        void AddPostDeployActions(PostDeployActions postDeployActions);

        /// <summary>
        /// Attempts to cancel an ongoing deployment. Create a new IArtifact to attempt a new deployment.
        /// </summary>
        /// <returns></returns>
        void CancelDeployment();

        /// <summary>
        /// Deploys and artifact to the system and waits until timeout for deployment.
        /// </summary>
        /// <returns><c>true</c> if deployment was successful; otherwise, <c>false</c>.</returns>
        Task<bool> DeployAsync(TimeSpan timeout);
    }
}