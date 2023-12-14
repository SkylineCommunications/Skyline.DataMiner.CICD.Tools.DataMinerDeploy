namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	public interface IDataMinerService
	{
		void InstallNewStyleAppPackages(string packageFilePath);
		void InstallOldStyleAppPackages(string package);
	}
}