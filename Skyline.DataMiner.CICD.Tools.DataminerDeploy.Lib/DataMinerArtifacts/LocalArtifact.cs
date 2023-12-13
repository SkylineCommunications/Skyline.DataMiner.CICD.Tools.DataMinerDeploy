namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Threading.Tasks;

	using Microsoft.Extensions.Logging;

	internal class LocalArtifact : IArtifact
	{
		private readonly ILogger _logger;
		private readonly string dataminerPassword;
		private readonly string dataMinerServerLocation;
		private readonly string dataminerUser;
		private readonly string pathToArtifact;

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, string dataminerUser, string dataminerPassword, ILogger logger)
		{
			this.pathToArtifact = pathToArtifact;
			this.dataMinerServerLocation = dataMinerServerLocation;
			this.dataminerUser = dataminerUser;
			this.dataminerPassword = dataminerPassword;
			this._logger = logger;
		}

		public void CancelDeployment()
		{
			throw new NotImplementedException();
		}

		public Task<bool> DeployAsync(TimeSpan timeout)
		{
			throw new NotImplementedException();
		}
	}
}