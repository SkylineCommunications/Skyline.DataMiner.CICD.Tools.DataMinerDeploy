namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	internal interface IDataMinerService
	{
		void InstallNewStyleAppPackages(string packageFilePath);
		void InstallOldStyleAppPackages(string package);
	}
}