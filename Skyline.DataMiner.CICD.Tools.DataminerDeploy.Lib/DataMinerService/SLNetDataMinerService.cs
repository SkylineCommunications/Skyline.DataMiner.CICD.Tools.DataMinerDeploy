﻿namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Linq;
	using System.Net;
	using System.Text;
	using System.Xml.Linq;

	using Alphaleonis.Win32.Filesystem;

	using ICSharpCode.SharpZipLib.Zip;

	using Microsoft.Extensions.Logging;

	using Skyline.DataMiner.CICD.FileSystem;
	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.RemotingConnection;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.AppPackages;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Net.Messages;

	internal class SLNetDataMinerService : IDisposable, IDataMinerService
	{
		private readonly IFileSystem fs;
		private readonly ILogger logger;
		private bool disposedValue = false;
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
				throw new InvalidOperationException("Please call .Connect before installing any packages.");
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
			catch
			{
				// ignore
			}

			if (uploadDataMinerProtocolResponse != null)
			{
				if (!String.IsNullOrWhiteSpace(uploadDataMinerProtocolResponse.ErrorMessage))
				{
					throw new InvalidOperationException($"Completed processing protocol package with error: {uploadDataMinerProtocolResponse.ErrorMessage}");
				}
				else
				{
					logger.LogDebug("Finished Installation of protocol package.");
				}
			}
			else
			{
				logger.LogDebug("Unable to process protocol package.");
			}
		}

		public void InstallNewStyleAppPackages(string packageFilePath)
		{
			if (slnet == null)
			{
				throw new InvalidOperationException("Please call .Connect before installing any packages.");
			}

			var helper = new AppPackageHelper(slnet.SendMessage);
			logger.LogDebug($"Cleaning previous uploaded packages with same name and version...");
			(string appName, string appVersion, int appBuild) = LoadAppPackage(packageFilePath);
			int cleaned = CleanPreviousUploaded(helper, appName, appVersion);

			logger.LogDebug($"Cleaning {cleaned} previous uploaded packages with same name ({appName}) and version ({appVersion})");
			logger.LogDebug($"Uploading {appName} to {slnet.EndPoint}...");
			string id = helper.UploadAppPackage(packageFilePath);
			logger.LogDebug($"Finished Uploading. Starting Installation...");
			helper.InstallApp(id);
			logger.LogDebug("Finished Installation of application package.");
		}

		public void InstallLegacyStyleAppPackages(string package, TimeSpan timeout)
		{
			if (slnet == null)
			{
				throw new InvalidOperationException("Please call .Connect before installing any packages.");
			}

			VerifyMinimumDataMinerVersion();

			using (var manager = new Skyline.DataMiner.Net.Upgrade.UpgradeExecuteManager())
			{
				// Path
				manager.SetUpgradePackageLocation(package);

				// SLNet connection
				manager.OnResolveConnection += OnResolveConnection;

				// Options
				var upgradeOptions = new Skyline.DataMiner.Net.Messages.UpgradeOptions();
				upgradeOptions.KillInternetExplorer = Skyline.DataMiner.Net.Messages.TriStateBool.False;
				upgradeOptions.AutoStartDataMinerOnStartup = Skyline.DataMiner.Net.Messages.TriStateBool.True;
				upgradeOptions.DelayStartDataMinerOnStartup = Skyline.DataMiner.Net.Messages.TriStateBool.True;
				upgradeOptions.ExtractAllFiles = Skyline.DataMiner.Net.Messages.TriStateBool.True;
				upgradeOptions.FailoverUpgradePolicy = Skyline.DataMiner.Net.Messages.FailoverAgentUpgradePolicy.UseDefault;
				upgradeOptions.FailoverUpgradePolicyOptions = Skyline.DataMiner.Net.Messages.FailoverAgentUpgradePolicyOptions.UseDefault;
				upgradeOptions.RebootAfterUpgrade = Skyline.DataMiner.Net.Messages.TriStateBool.False;
				upgradeOptions.SkipSNMP = Skyline.DataMiner.Net.Messages.TriStateBool.True;

				manager.SetUpgradeOptions(upgradeOptions);

				// Translate domain name to ip if possible
				string ip = slnet.EndPoint;

				// Note, this replaces the default slnet.Connection.ResolvedConnectIP that won't work in .NETStandard
				var ipAddress = Dns.GetHostAddresses(slnet.EndPoint).FirstOrDefault();
				if (ipAddress != null)
				{
					ip = ipAddress.ToString();
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

				if (!upgradeOk)
				{
					throw new TimeoutException("DMA upgrade did not complete within " + timeout + " minutes. See 'Upgrade Log Table' for more info.");
				}
				else
				{
					logger.LogDebug("Finished Installation of application package.");
				}
			}
		}

		public bool TryConnect(string dmaIp, string dmaUser, string dmaPass)
		{
			if (slnet == null)
			{
				slnet = SLNetCommunication.GetConnection(dmaIp, dmaUser, dmaPass);
				slnet.Connection.PollingRequestTimeout = 120000;
				slnet.Connection.ConnectTimeoutTime = 120000;
				slnet.Connection.AuthenticateMessageTimeout = 120000;
				return true;
			}
			else
			{
				return false;
			}
		}

		// To detect redundant calls
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
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

			if (uploadedAppPackages != null)
			{
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
			}

			return cleanedCount;
		}

		private string GetAgentVersion()
		{
			try
			{
				GetAgentBuildInfo buildInfoMessage = new GetAgentBuildInfo();
				BuildInfoResponse buildInfoResponse = (BuildInfoResponse)slnet.SendSingleResponseMessage(buildInfoMessage);

				if (buildInfoResponse != null && buildInfoResponse.Agents.Length > 0)
				{
					string rawVersion = buildInfoResponse.Agents[0].RawVersion;
					return rawVersion;
				}
			}
			catch (DataMinerException e)
			{
				logger.LogDebug("(ignoring) Could not retrieve DataMiner Version for requirement checks. Exception: " + e);
				// best effort, still try the installation.
			}

			return String.Empty;
		}

		/// <summary>
		/// Verifies if the provided version number is higher then the DataMiner Agent version.
		/// </summary>
		/// <param name="expectedMinimum">The expected minimum version.</param>
		/// <param name="version">The version to compare.</param>
		/// <returns><c>true</c> if the provided version is higher than the specified minimum version number; otherwise, <c>false</c>.</returns>
		private bool IsVersionHigherOrEqual(string expectedMinimum, string version)
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
				int[] versionParts = ParseVersionNumbers(version);
				int[] minimumParts = ParseVersionNumbers(expectedMinimum);

				for (int i = 0; i < 4; i++)
				{
					int versionPart = versionParts[i];
					int minimumVersionPart = minimumParts[i];
					if (versionPart > minimumVersionPart)
					{
						return true;
					}
				}
			}
			catch (Exception e)
			{
				logger.LogDebug("(ignoring) Could not verify DataMiner Minimum Version requirement. Exception: " + e);
				// best effort, still try the installation.
				return true;
			}

			return false;
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
				string name;
				string version;

				var appInfoXml = XDocument.Parse(appInfoSb.ToString());
				string buildStr = appInfoXml?.Root?.Element("Build")?.Value;
				int tryParsedAppBuild;
				if (buildStr != null && Int32.TryParse(buildStr, out tryParsedAppBuild))
				{
					build = tryParsedAppBuild;
				}
				else
				{
					build = 0;
				}

				name = appInfoXml?.Root?.Element("Name")?.Value;
				version = appInfoXml?.Root?.Element("Version")?.Value;

				return (name, version, build);
			}
		}

		private void OnResolveConnection(object sender, Skyline.DataMiner.Net.Upgrade.ResolveConnectionArgs e)
		{
			if (e.DestinationIPAddress.Equals("localhost") || e.DestinationIPAddress.Equals("127.0.0.1"))
			{
				e.Connection = null;
				return;
			}

			e.Connection = slnet.Connection;
		}

		private void OnUpdate(object sender, Skyline.DataMiner.Net.Upgrade.UpgradeWatcherLogEventArgs e)
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

			string minimumRequired = "10.3.0.0";
			var remoteAgentVersion = GetAgentVersion();

			if (!String.IsNullOrWhiteSpace(remoteAgentVersion) && !IsVersionHigherOrEqual(minimumRequired, remoteAgentVersion))
			{
				throw new InvalidOperationException($"Cannot install legacy application package. Current DataMiner version {remoteAgentVersion} is not higher or equal to the minimum required DataMiner version {minimumRequired}. Please upgrade your agent to use this call.");
			}
			else
			{
				logger.LogDebug($"Checking Requirements: OK, DataMiner version {remoteAgentVersion} is higher or equal to the minimum required version {minimumRequired}...");
			}
		}
	}
}