namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Reflection;
	using System.Text;
	using System.Xml.Linq;

	using Alphaleonis.Win32.Filesystem;

	using ICSharpCode.SharpZipLib.Zip;

	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.RemotingConnection;
	using Skyline.DataMiner.Net.AppPackages;

	internal class SLNetDataMinerService : IDisposable, IDataMinerService
	{
		private SLNetCommunication slnet;

		private readonly string dmaDllsFilesLocations;

		public SLNetDataMinerService(string dmaDllsFilesLocations)
		{
			this.dmaDllsFilesLocations = dmaDllsFilesLocations;

			var currentDomain = AppDomain.CurrentDomain;
			currentDomain.AssemblyResolve += new ResolveEventHandler(MyResolveEventHandler);
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

		private Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
		{
			Assembly result = null;

			if (Directory.Exists("C:\\Skyline DataMiner\\Files"))
			{
				result = GetAssembly(args, "C:\\Skyline DataMiner\\Files\\");
			}

			if (result == null)
			{
				if (String.IsNullOrWhiteSpace(dmaDllsFilesLocations))
				{
					result = GetAssembly(args, "D:\\DataMiner dlls\\LatestFeature\\Files");
				}
				else
				{
					result = GetAssembly(args, dmaDllsFilesLocations);
				}
			}

			return result;
		}

		private static Assembly GetAssembly(ResolveEventArgs args, string validatorFilesFolderPath)
		{
			var missingAssemblyName = new AssemblyName(args.Name);
			var assemblyFilePath = Path.Combine(validatorFilesFolderPath, missingAssemblyName.Name + ".dll");
			if (File.Exists(assemblyFilePath))
			{
				try
				{
					var assembly = Assembly.LoadFile(assemblyFilePath);
					return assembly;
				}
				catch (Exception ex)
				{
					Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' could not be loaded. Exception thrown:" + Environment.NewLine + ex.Message + Environment.NewLine + "Trying again via bytes.");

					try
					{
						var assembly = Assembly.Load(File.ReadAllBytes(assemblyFilePath));
						return assembly;
					}
					catch (Exception e)
					{
						Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' could not be loaded via bytes. Exception thrown:" + Environment.NewLine + e);
					}

					// Ignore
				}
			}
			else
			{
				Console.WriteLine("MyResolveEventHandler|assemblyFilePath '" + assemblyFilePath + "' could not be loaded. The file doesn't exist.");
			}

			return null;
		}

		private void CleanPreviousUploaded(AppPackageHelper helper, string appName, string version, int build)
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

		public void InstallNewStyleAppPackages(string packageFilePath)
		{
			if (slnet == null)
			{
				throw new InvalidOperationException("Please call .Connect before installing any packages.");
			}

				var helper = new AppPackageHelper(slnet.SendMessage);
			var allPackages = helper.GetUploadedAppPackages();
			(string appName, string appVersion, int appBuild) = LoadAppPackage(packageFilePath);
			CleanPreviousUploaded(helper, appName, appVersion, appBuild);

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

				// Stop SLNet connection
				slnet.Dispose();
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

		private static void OnUpdate(object sender, Skyline.DataMiner.Net.Upgrade.UpgradeWatcherLogEventArgs e)
		{
			Console.WriteLine(e.ProgressInfo.Message);
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

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

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}
		#endregion
	}
}
