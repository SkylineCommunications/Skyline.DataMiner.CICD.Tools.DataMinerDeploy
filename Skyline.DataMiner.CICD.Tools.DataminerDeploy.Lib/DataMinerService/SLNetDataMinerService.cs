namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Xml.Linq;

    using Alphaleonis.Win32.Filesystem;

    using ICSharpCode.SharpZipLib.Zip;

    using Microsoft.Extensions.Logging;

    using Skyline.DataMiner.CICD.Common;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService.SLNet;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.AppPackages;
    using Skyline.DataMiner.Net.Exceptions;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Upgrade;

    internal class SLNetDataMinerService : IDataMinerService
    {
        private readonly IFileSystem fs;
        private readonly ILogger logger;
        private bool disposedValue;
        private SLNetCommunication slnet;

        public SLNetDataMinerService(IFileSystem fs, ILogger logger)
        {
            this.logger = logger;
            this.fs = fs;
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void InstallDataMinerProtocol(string protocol)
        {
            if (slnet == null)
            {
                throw new InvalidOperationException($"Please call {nameof(TryConnect)} before installing any packages.");
            }

            if (protocol == null)
            {
                throw new ArgumentNullException(nameof(protocol));
            }

            logger.LogDebug($"Uploading {protocol} to {slnet.EndPoint}...");
            FileUploader fileUploader = new FileUploader(slnet.Connection);

            var contents = fs.File.ReadAllBytes(protocol);
            int dmprotocolCookie = fileUploader.SendBytes(contents, fs.Path.GetFileNameWithoutExtension(protocol));

            UploadDataMinerProtocolResponse uploadDataMinerProtocolResponse = null;
            logger.LogDebug($"Finished Uploading. Starting Installation...");

            try
            {
                UploadDataMinerProtocolMessage uploadDataMinerProtocolMessage = new UploadDataMinerProtocolMessage(dmprotocolCookie);
                uploadDataMinerProtocolResponse = slnet.SendSingleResponseMessage(uploadDataMinerProtocolMessage) as UploadDataMinerProtocolResponse;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Unable to process the protocol package in DataMiner with exception: " + e.Message, e);
            }

            if (uploadDataMinerProtocolResponse != null)
            {
                if (!String.IsNullOrWhiteSpace(uploadDataMinerProtocolResponse.ErrorMessage))
                {
                    throw new InvalidOperationException($"Completed processing protocol package with error: {uploadDataMinerProtocolResponse.ErrorMessage}");
                }
                else
                {
                    logger.LogDebug($"Finished Installation of Connector {protocol}.");
                }
            }
            else
            {
                logger.LogError("Unable to process protocol package. Returned response from DataMiner was null.");
            }
        }

        public void InstallNewStyleAppPackages(string packageFilePath)
        {
            if (slnet == null)
            {
                throw new InvalidOperationException($"Please call {nameof(TryConnect)} before installing any packages.");
            }

            var helper = new AppPackageHelper(slnet.SendMessage);
            logger.LogDebug($"Cleaning previous uploaded packages with same name and version...");
            (string appName, string appVersion, _) = LoadAppPackage(packageFilePath);
            int cleaned = CleanPreviousUploaded(helper, appName, appVersion);

            logger.LogDebug($"Cleaning {cleaned} previous uploaded packages with same name ({appName}) and version ({appVersion})");
            logger.LogDebug($"Uploading {appName} to {slnet.EndPoint}...");
            string id = helper.UploadAppPackage(packageFilePath);
            logger.LogDebug($"Finished Uploading. Starting Installation...");
            helper.InstallApp(id);
            logger.LogDebug($"Finished Installation of application package with name ({appName}) and version ({appVersion}).");
        }

        public void InstallLegacyStyleAppPackages(string package, TimeSpan timeout)
        {
            if (slnet == null)
            {
                throw new InvalidOperationException($"Please call {nameof(TryConnect)} before installing any packages.");
            }

            VerifyMinimumDataMinerVersion();

            using (var manager = new UpgradeExecuteManager())
            {
                // Path
                manager.SetUpgradePackageLocation(package);

                // SLNet connection
                manager.OnResolveConnection += OnResolveConnection;

                // Options
                var upgradeOptions = new UpgradeOptions
                {
                    KillInternetExplorer = TriStateBool.False,
                    AutoStartDataMinerOnStartup = TriStateBool.True,
                    DelayStartDataMinerOnStartup = TriStateBool.True,
                    ExtractAllFiles = TriStateBool.True,
                    FailoverUpgradePolicy = FailoverAgentUpgradePolicy.UseDefault,
                    FailoverUpgradePolicyOptions = FailoverAgentUpgradePolicyOptions.UseDefault,
                    RebootAfterUpgrade = TriStateBool.False,
                    SkipSNMP = TriStateBool.True
                };

                manager.SetUpgradeOptions(upgradeOptions);

                // Translate domain name to ip if possible
                string ip = slnet.EndPoint;

                // Note, this replaces the default slnet.Connection.ResolvedConnectIP that won't work in .NETStandard
                var ipAddress = Dns.GetHostAddresses(slnet.EndPoint).FirstOrDefault();
                if (ipAddress != null)
                {
                    ip = ipAddress.ToString();
                }

                if(ip == "127.0.0.1")
                {
                    throw new InvalidOperationException("Unsupported: Legacy style local artifact deployment to localhost. As an alternative, please deploy from the catalog or deploy from a remote server.");
                }

                var watcher = manager.AddStandAloneAgentToUpgrade(ip);

                // Callback
                watcher.OnUpdate += OnUpdate;

                // Start update and wait
                bool upgradeOk = false;

                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    watcher.StartWatching();
                    logger.LogDebug($"Installing {package} to {slnet.EndPoint} and restarting the agent...");
                    manager.LaunchUploadAndUpgrade();
                    watcher.WaitForUpgradeCompletion();
                    watcher.StopWatching();
                    upgradeOk = true;
                });
                task.Wait(timeout);

#pragma warning disable S2583 // Conditionally executed code should be reachable <--- Is set to true inside of the task
                if (!upgradeOk)
                {
                    throw new TimeoutException("DMA upgrade did not complete within " + timeout + " minutes. See 'Upgrade Log Table' for more info.");
                }
                else
                {
                    logger.LogDebug($"Finished Installation and agent restart for legacy application package {package}.");
                }
#pragma warning restore S2583 // Conditionally executed code should be reachable
            }
        }

        public bool TryConnect(string dmaIp, string dmaUser, string dmaPass)
        {
            if (slnet != null)
            {
                return false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new InvalidOperationException("Unsupported on Linux: Deployment of local artifacts. Please run this on a windows system or use the deployment from catalog as an alternative.");
            }

            slnet = SLNetCommunication.GetConnection(dmaIp, dmaUser, dmaPass);
            return true;
        }

        // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && slnet != null)
                {
                    slnet.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Parses the version number string into a string array.
        /// </summary>
        /// <param name="versionNumber">The version number.</param>
        /// <returns>String array containing the parsed version number.</returns>
        /// <exception cref="ArgumentException">When the version number is not in the expected format of a.b.c.d where a,b,c and d are integers.</exception>
        private static int[] ParseVersionNumbers(string versionNumber)
        {
            string[] splitDot = { "." };
            string[] versionParts = versionNumber.Split(splitDot, StringSplitOptions.None);
            if (versionParts.Length != 4)
            {
                throw new ArgumentException($"versionNumber {versionNumber} is not in expected format.");
            }

            if (!Int32.TryParse(versionParts[0], out int versionPartMajor) ||
                !Int32.TryParse(versionParts[2], out int versionPartMonth) ||
                !Int32.TryParse(versionParts[1], out int versionPartMinor) ||
                !Int32.TryParse(versionParts[3], out int versionPartWeek))
            {
                throw new ArgumentException($"versionNumber {versionNumber} is not in expected format.");
            }

            int[] versionPartNumbers = new int[4];
            versionPartNumbers[0] = versionPartMajor;
            versionPartNumbers[1] = versionPartMinor;
            versionPartNumbers[2] = versionPartMonth;
            versionPartNumbers[3] = versionPartWeek;

            return versionPartNumbers;
        }

        private int CleanPreviousUploaded(AppPackageHelper helper, string appName, string version)
        {
            var uploadedAppPackages = helper.GetUploadedAppPackages();
            int cleanedCount = 0;

            if (uploadedAppPackages == null)
            {
                return cleanedCount;
            }

            foreach (var uploadedApp in uploadedAppPackages)
            {
                bool sameName = uploadedApp.AppInfo.Name.Equals(appName, StringComparison.InvariantCulture);
                bool sameVersion = uploadedApp.AppInfo.Version.Equals(version, StringComparison.InvariantCulture);

                if (sameName && sameVersion)
                {
                    cleanedCount++;
                    helper.RemoveUploadedAppPackage(uploadedApp.UploadedPackageID);
                }
            }

            return cleanedCount;
        }

        private DataMinerVersion GetAgentVersion()
        {
            try
            {
                GetAgentBuildInfo buildInfoMessage = new GetAgentBuildInfo();
                BuildInfoResponse buildInfoResponse = (BuildInfoResponse)slnet.SendSingleResponseMessage(buildInfoMessage);

                if (buildInfoResponse != null && buildInfoResponse.Agents.Length > 0)
                {
                    var agentInfo = buildInfoResponse.Agents[0];
                    Version version = new Version(agentInfo.VersionPartMajor, agentInfo.VersionPartMinor, agentInfo.VersionPartMonth, agentInfo.VersionPartWeek);
                    var rawVersion = new DataMinerVersion(version, (uint)agentInfo.UpgradeBuildID);
                    return rawVersion;
                }
            }
            catch (DataMinerException e)
            {
                logger.LogDebug("(ignoring) Could not retrieve DataMiner Version for requirement checks. Exception: " + e);
                // best effort, still try the installation.
            }

            return null;
        }

        /// <summary>
        /// Verifies if the provided version number is higher then the DataMiner Agent version.
        /// </summary>
        /// <param name="expectedMinimum">The expected minimum version.</param>
        /// <param name="version">The version to compare.</param>
        /// <returns><c>true</c> if the provided version is higher than the specified minimum version number; otherwise, <c>false</c>.</returns>
        private bool IsVersionHigherOrEqual(DataMinerVersion expectedMinimum, DataMinerVersion version)
        {
            if (expectedMinimum == null || version == null)
            {
                return false;
            }

            if (expectedMinimum == version)
            {
                return true;
            }

            try
            {
                return version >= expectedMinimum;
            }
            catch (Exception e)
            {
                logger.LogDebug("(ignoring) Could not verify DataMiner Minimum Version requirement. Exception: " + e);
                // best effort, still try the installation.
                return true;
            }
        }

        private (string appName, string appVersion, int appBuild) LoadAppPackage(string fullPath)
        {
            using (var fileStream = File.Open(fullPath, System.IO.FileMode.Open))
            using (var zip = new ZipFile(fileStream))
            {
                var descriptionEntry = zip.GetEntry("Description.txt");
                var appInfoEntry = zip.GetEntry("AppInfo.xml");

                StringBuilder appInfoSb = new StringBuilder();
                if (appInfoEntry != null && appInfoEntry.IsFile)
                {
                    System.IO.Stream stream = zip.GetInputStream(appInfoEntry);
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(stream))
                    {
                        appInfoSb.AppendLine(reader.ReadToEnd());
                    }
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(appInfoSb);
                if (descriptionEntry != null && descriptionEntry.IsFile)
                {
                    System.IO.Stream stream = zip.GetInputStream(descriptionEntry);
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(stream))
                    {
                        sb.AppendLine(reader.ReadToEnd());
                    }
                }

                int build;

                var appInfoXml = XDocument.Parse(appInfoSb.ToString());
                string buildStr = appInfoXml?.Root?.Element("Build")?.Value;
                if (buildStr != null && Int32.TryParse(buildStr, out int tryParsedAppBuild))
                {
                    build = tryParsedAppBuild;
                }
                else
                {
                    build = 0;
                }

                string name = appInfoXml?.Root?.Element("Name")?.Value;
                string version = appInfoXml?.Root?.Element("Version")?.Value;

                return (name, version, build);
            }
        }

        private void OnResolveConnection(object sender, ResolveConnectionArgs e)
        {
            if (e.DestinationIPAddress.Equals("localhost") || e.DestinationIPAddress.Equals("127.0.0.1"))
            {
                e.Connection = null;
                return;
            }

            e.Connection = slnet.Connection;
        }

        private void OnUpdate(object sender, UpgradeWatcherLogEventArgs e)
        {
            logger.LogDebug(e.ProgressInfo.Message);
        }

        private void VerifyMinimumDataMinerVersion()
        {
            // This has a minimum DataMiner requirement:
            // https://dms-it-dev.skyline.local/apigateway/api/version
            // need
            //	APIGateway versie >"0.0.1.52414"
            //	SLNetUpgradeService > "10.3.2243.10098"
            //	SLNetUpgradeService > 10.3.2317.354

            // Should verify the remote agent first, then make a new clean error message otherwise u get an unclear exception from slnet.
            // minimum dataminer version: https://intranet.skyline.be/DataMiner/Lists/Release%20Notes/DispForm2.aspx?ID=36023
            //		Main Release Version
            //		10.3.0[CU3]
            //		Feature Release Version
            //		10.3.6
            //		Internal Build ID
            //		10.3.0.0 - 12948
            // Note you need: .NET Hosting Bundle before upgrading agent, otherwise it won't be able to update the APIGateway

            // Let's avoid needing to make a different connection. Let's re-use the SLNetCommunication here.
            // Assuming there might be corner cases where retrieve the version won't work.
            // At that case we'll best effort try the upgrade and let any exceptions bubble up from the core if they would happen.
            // The goal of the validation is to stop the code (the core software will do that), it's to make a clear error message to the user if we detect an invalid version.

            Version v = new Version("10.3.0.0");
            DataMinerVersion minimumRequired = new DataMinerVersion(v, 12948);
            var remoteAgentVersion = GetAgentVersion();

            if (remoteAgentVersion != null && !IsVersionHigherOrEqual(minimumRequired, remoteAgentVersion))
            {
                throw new InvalidOperationException($"Cannot install legacy application package. Current DataMiner version {remoteAgentVersion} is not higher or equal to the minimum required DataMiner version {minimumRequired}. Please upgrade your agent to use this call.");
            }
            else
            {
                logger.LogDebug($"Checking Requirements: OK, DataMiner version {remoteAgentVersion?.ToString() ?? "unknown"} is higher or equal to the minimum required version {minimumRequired}...");
            }
        }
    }
}