namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;

	public class DeployedPackage
	{
		public string Status { get; set; }

		public DeployedPackage(string status)
		{
			Status = status;
		}
	}
}