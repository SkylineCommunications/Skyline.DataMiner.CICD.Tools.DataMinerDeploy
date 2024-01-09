namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;

	/// <summary>
	/// Represents a service that can connect and perform installations to a DataMiner agent.
	/// </summary>
	public interface IDataMinerService
	{
		/// <summary>
		/// Install a DataMiner protocol (aka Connector, driver) package (.dmprotocol). This call will wait on the installation to complete.
		/// </summary>
		/// <param name="protocol">The file path to the package.</param>
		void InstallDataMinerProtocol(string protocol);

		/// <summary>
		/// Install a DataMiner Application Package (.dmapp). This call will wait on the installation to complete.
		/// </summary>
		/// <param name="packageFilePath">The file path to the package.</param>
		void InstallNewStyleAppPackages(string packageFilePath);

		/// <summary>
		/// Install a legacy style Application package (.dmapp when unzipped has an Upgrade.zip inside). This call will wait on the installation to complete.
		/// </summary>
		/// <remarks>Running this call will restart the DataMiner Agent.</remarks>
		/// <param name="package">The file path to the package.</param>
		/// <param name="timeout">A timeout time to wait on the installation and restart of the agent.</param>
		void InstallLegacyStyleAppPackages(string package, TimeSpan timeout);

		/// <summary>
		/// Try to connect to a DataMiner agent using the host name or IP address. This should be called before attempting an installation.
		/// </summary>
		/// <param name="dmaIp">The hostname or IP of the DataMiner agent.</param>
		/// <param name="dmaUser">The name of a DataMiner user for authentication.</param>
		/// <param name="dmaPass">The password of a DataMiner user for authentication.</param>
		/// <returns>If the connection was successfully established or not.</returns>
		bool TryConnect(string dmaIp, string dmaUser, string dmaPass);
	}
}