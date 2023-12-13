namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	public class DeployedPackage
	{
		public DeployedPackage(string status)
		{
			Status = status;
		}

		public string Status { get; set; }
	}
}