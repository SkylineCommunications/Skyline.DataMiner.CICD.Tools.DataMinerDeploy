using Microsoft.Extensions.Logging;

namespace Skyline.DataMiner.CICD.Tools.DataminerDeploy.Lib
{
	/// <summary>
	/// Creates an instance of <see cref="IArtifact" that can be used for deployment to a DataMiner./>
	/// </summary>
	public static class DeploymentFactory
	{
		/// <summary>
		/// A deployment using a local package to a local network connected DataMiner agent.
		/// </summary>
		/// <param name="pathToArtifact"></param>
		/// <param name="dataMinerServerLocation"></param>
		/// <param name="dataminerUser"></param>
		/// <param name="dataminerPassword"></param>
		/// <returns></returns>
		public static IArtifact Local(string pathToArtifact, string dataMinerServerLocation, string dataminerUser, string dataminerPassword, ILogger logger)
		{
			return new LocalArtifact(pathToArtifact, dataMinerServerLocation, dataminerUser, dataminerPassword, logger);
		}

		/// <summary>
		/// A deployment using a catalog artifact identifier from the cloud, to a cloud connected agent.
		/// </summary>
		/// <param name="artifactIdentifier"></param>
		/// <param name="catalogAgentToken"></param>
		/// <returns></returns>
		public static IArtifact Cloud(string artifactIdentifier, string catalogAgentToken, ILogger logger)
		{
			return new CatalogArtifact(artifactIdentifier, catalogAgentToken, logger);
		}
	}
}
