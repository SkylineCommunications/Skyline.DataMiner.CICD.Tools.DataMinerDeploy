namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;

	public class DeployingPackage
	{
		public string ArtifactId { get; }
		public Guid DeploymentId { get; }

		public DeployingPackage(string artifactId, Guid deploymentId)
		{
			ArtifactId = artifactId;
			DeploymentId = deploymentId;
		}
	}
}