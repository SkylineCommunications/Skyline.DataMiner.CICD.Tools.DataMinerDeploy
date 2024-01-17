namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService
{
    using System;

    /// <summary>
    /// Represents a service that can connect and perform installations to a DataMiner Agent.
    /// </summary>
    public interface IDataMinerService: IDisposable
    {
        /// <summary>
        /// Installs a DataMiner protocol (aka connector, driver) package (.dmprotocol). This call will wait on the installation to complete.
        /// </summary>
        /// <param name="protocol">The file path to the package.</param>
        void InstallDataMinerProtocol(string protocol);

        /// <summary>
        /// Install a DataMiner application package (.dmapp). This call will wait on the installation to complete.
        /// </summary>
        /// <param name="packageFilePath">The file path to the package.</param>
        void InstallNewStyleAppPackages(string packageFilePath);

        /// <summary>
        /// Install a legacy style application package (.dmapp when unzipped has an Upgrade.zip inside). This call will wait on the installation to complete.
        /// </summary>
        /// <remarks>Running this call will restart the DataMiner Agent.</remarks>
        /// <param name="package">The file path to the package.</param>
        /// <param name="timeout">A timeout time to wait on the installation and restart of the Agent.</param>
        void InstallLegacyStyleAppPackages(string package, TimeSpan timeout);

        /// <summary>
        /// Try to connect to a DataMiner Agent using the host name or IP address. This should be called before attempting an installation.
        /// </summary>
        /// <param name="dmaIp">The host name or IP address of the DataMiner Agent.</param>
        /// <param name="dmaUser">The name of a DataMiner user for authentication.</param>
        /// <param name="dmaPass">The password of a DataMiner user for authentication.</param>
        /// <returns><c>true</c> if the connection was successfully established; otherwise, <c>false</c>.</returns>
        bool TryConnect(string dmaIp, string dmaUser, string dmaPass);
    }
}