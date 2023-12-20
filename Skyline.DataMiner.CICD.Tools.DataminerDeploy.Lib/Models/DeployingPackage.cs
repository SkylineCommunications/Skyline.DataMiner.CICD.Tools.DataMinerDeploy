namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;

	/// <summary>
	/// Instances of <see cref="DeployingPackage"/> represent the deployment action getting performed.
	/// </summary>
	public class DeployingPackage
	{
		/// <summary>
		/// Creates instances of <see cref="DeployingPackage"/>.
		/// </summary>
		/// <param name="artifactId">The unique identifier of the artifact.</param>
		/// <param name="deploymentId">The unique ID representing the deployment action.</param>
		public DeployingPackage(string artifactId, Guid deploymentId)
		{
			ArtifactId = artifactId;
			DeploymentId = deploymentId;
		}

		/// <summary>
		/// The unique identifier of the artifact.
		/// </summary>
		public string ArtifactId { get; }

		/// <summary>
		/// The unique ID representing the deployment action.
		/// </summary>
		public Guid DeploymentId { get; }
	}
}