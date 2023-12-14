namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Text;
	using System.Xml.Linq;

	using Alphaleonis.Win32.Filesystem;

	using ICSharpCode.SharpZipLib.Zip;

	using Microsoft.Extensions.Logging;

	using Skyline.DataMiner.CICD.FileSystem;
	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.RemotingConnection;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.AppPackages;
	using Skyline.DataMiner.Net.Messages;

	internal class SLNetDataMinerService : IDisposable, IDataMinerService
	{
		private SLNetCommunication slnet;
		private readonly IFileSystem fs;
		private readonly ILogger logger;

		public SLNetDataMinerService(IFileSystem fs, ILogger logger)
		{
			this.logger = logger;
			this.fs = fs;
		}

		public void InstallDataminerProtocol(string protocol)
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
					throw new InvalidOperationException($"Completed processing dmprotocol with error: {uploadDataMinerProtocolResponse.ErrorMessage}");
				}
				else
				{
					logger.LogDebug("Finished Installation of dmprotocol.");
				}
			}
			else
			{
				logger.LogDebug("Unable to process dmprotocol.");
			}
		}

		public void InstallNewStyleAppPackages(string packageFilePath)
		{
			if (slnet == null)
			{
				throw new InvalidOperationException("Please call .Connect before installing any packages.");
			}

			var helper = new AppPackageHelper(slnet.SendMessage);
			(string appName, string appVersion, int appBuild) = LoadAppPackage(packageFilePath);
			CleanPreviousUploaded(helper, appName, appVersion);

			string id = helper.UploadAppPackage(packageFilePath);
			helper.InstallApp(id);
		}

		public void InstallOldStyleAppPackages(string package)
		{
			if (slnet == null)
			{
				throw new InvalidOperationException("Please call .Connect before installing any packages.");
			}

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
				upgradeOptions.SkipSNMP = Skyline.DataMiner.Net.Messages.TriStateBool.False;
				manager.SetUpgradeOptions(upgradeOptions);

				// Translate domain name to ip if possible
				string ip = slnet.EndPoint;
				if (!String.IsNullOrWhiteSpace(slnet.Connection.ResolvedConnectIP))
				{
					ip = slnet.Connection.ResolvedConnectIP;
				}

				var watcher = manager.AddStandAloneAgentToUpgrade(ip);

				// Callback
				watcher.OnUpdate += OnUpdate;

				// Start update and wait
				bool upgradeOk = false;
				int timeout = 10;
				var task = System.Threading.Tasks.Task.Run(() =>
				{
					watcher.StartWatching();
					manager.LaunchUploadAndUpgrade();
					watcher.WaitForUpgradeCompletion();
					watcher.StopWatching();
					upgradeOk = true;
				});
				task.Wait(TimeSpan.FromMinutes(timeout));

				if (!upgradeOk)
				{
					throw new TimeoutException("DMA upgrade did not complete within " + timeout + " minutes. See 'Upgrade Log Table' for more info.");
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

		private static void OnUpdate(object sender, Skyline.DataMiner.Net.Upgrade.UpgradeWatcherLogEventArgs e)
		{
			Console.WriteLine(e.ProgressInfo.Message);
		}

		private void CleanPreviousUploaded(AppPackageHelper helper, string appName, string version)
		{
			var uploadedAppPackages = helper.GetUploadedAppPackages();
			if (uploadedAppPackages != null)
			{
				foreach (var uploadedApp in uploadedAppPackages)
				{
					bool sameName = uploadedApp.AppInfo.Name.Equals(appName, StringComparison.InvariantCulture);
					bool sameVersion = uploadedApp.AppInfo.Version.Equals(version, StringComparison.InvariantCulture);

					if (sameName && sameVersion)
					{
						helper.RemoveUploadedAppPackage(uploadedApp.UploadedPackageID);
					}
				}
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

		private bool disposedValue = false; // To detect redundant calls

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

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
	}
}