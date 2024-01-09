namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Runtime.InteropServices;
	using System.Threading.Tasks;

	using Microsoft.Extensions.Logging;

	using Skyline.DataMiner.CICD.FileSystem;
	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerArtifacts;
	using Skyline.DataMiner.Net;

	internal class LocalArtifact : IArtifact
	{
		private readonly ILogger _logger;
		private readonly string dataminerPassword;
		private readonly string dataMinerServerLocation;
		private readonly string dataminerUser;
		private readonly IFileSystem fs;
		private readonly string pathToArtifact;
		private readonly IDataMinerService service;
		private bool disposedValue;
		private string pwFromEnv;
		private string userFromEnv;

		public LocalArtifact(IDataMinerService dataminerService, string pathToArtifact, string dataMinerServerLocation, string dataMinerUser, string dataMinerPassword, ILogger logger, IFileSystem fs)
		{
			if (String.IsNullOrWhiteSpace(pathToArtifact))
			{
				throw new ArgumentNullException(nameof(pathToArtifact));
			}

			if (dataminerService == null)
			{
				throw new ArgumentNullException(nameof(dataminerService));
			}
			this.fs = fs;
			this.service = dataminerService;
			this.pathToArtifact = pathToArtifact;
			this.dataMinerServerLocation = dataMinerServerLocation;
			this.dataminerUser = dataMinerUser;
			this.dataminerPassword = dataMinerPassword;
			this._logger = logger;
			TryFindEnvironmentKeys();
		}

		public LocalArtifact(IDataMinerService dataMinerService, string pathToArtifact, string dataMinerServerLocation, ILogger logger, IFileSystem fs) : this(dataMinerService, pathToArtifact, dataMinerServerLocation, null, null, logger, fs)
		{
		}

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, string dataMinerUser, string dataMinerPassword, ILogger logger, IFileSystem fs) : this(DataMinerServiceFactory.CreateWithSLNet(fs, logger), pathToArtifact, dataMinerServerLocation, dataMinerUser, dataMinerPassword, logger, fs)
		{
		}

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, ILogger logger, IFileSystem fs) : this(pathToArtifact, dataMinerServerLocation, null, null, logger, fs)
		{
		}

		public void CancelDeployment()
		{
			throw new InvalidOperationException("Unable to cancel deployment of a local artifact.");
		}

		public async Task<bool> DeployAsync(TimeSpan timeout)
		{
			return Task.Factory.StartNew(() =>
			{
				if (!fs.File.Exists(pathToArtifact))
				{
					throw new InvalidOperationException($"Unable to deploy, path does not exist: {pathToArtifact}");
				}

				var actualUser = String.IsNullOrWhiteSpace(dataminerUser) ? userFromEnv : dataminerUser;
				var actualPassword = String.IsNullOrWhiteSpace(dataminerPassword) ? pwFromEnv : dataminerPassword;
				if (String.IsNullOrEmpty(actualUser) || String.IsNullOrEmpty(actualPassword))
				{
					throw new InvalidOperationException("Username or password is empty. Expected credentials either provided through arguments or with Environment Variables DATAMINER_DEPLOY_USER_ENCRYPTED/DATAMINER_DEPLOY_PASSWORD_ENCRYPTED or DATAMINER_DEPLOY_USER/DATAMINER_DEPLOY_PASSWORD.");
				}

				_logger.LogDebug($"Setting up connection to {dataMinerServerLocation}...");
				service.TryConnect(dataMinerServerLocation, actualUser, actualPassword);
				_logger.LogDebug($"Connection established.");
				ArtifactType type = new ArtifactType(fs, pathToArtifact);

				switch (type.Value)
				{
					case ArtifactTypeEnum.dmapp:
						_logger.LogDebug($"Found DataMiner application installation package (.dmapp).");
						service.InstallNewStyleAppPackages(pathToArtifact);
						break;

					case ArtifactTypeEnum.legacyDmapp:
						_logger.LogDebug($"Found legacy DataMiner application installation package (.dmapp).");
						service.InstallLegacyStyleAppPackages(pathToArtifact, timeout);
						break;

					case ArtifactTypeEnum.dmprotocol:
						_logger.LogDebug($"Found DataMiner protocol package (.dmprotocol).");
						service.InstallDataMinerProtocol(pathToArtifact);
						break;

					default:
						break;
				}

				return true;
			}).Wait(timeout);
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// Nothing to dispose right now
					//cancellationTokenSource.Dispose();
				}

				disposedValue = true;
			}
		}

		/// <summary>
		///  Attempts to find the necessary API key in Environment Variables. In order of priority:
		///  <para>- key stored as an Environment Variable called "dmcatalogtoken". (unix/win)</para>
		///  <para>- key configured using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys called "dmcatalogtoken_encrypted" (windows only)</para>
		/// </summary>
		private void TryFindEnvironmentKeys()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try
				{
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("DATAMINER_DEPLOY_USER_ENCRYPTED");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(string.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_DEPLOY_USER_ENCRYPTED' created by WinEncryptedKeys.");
							userFromEnv = keyFromWinEncryptedKeys;
						}
					}
				}
				catch (InvalidOperationException)
				{
					// Gobble up, no key means we try the next thing.
				}

				try
				{
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(string.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_DEPLOY_PASSWORD_ENCRYPTED' created by WinEncryptedKeys.");
							pwFromEnv = keyFromWinEncryptedKeys;
						}
					}
				}
				catch (InvalidOperationException)
				{
					// Gobble up, no key means we try the next thing.
				}
			}

			string userFromEnvironment = Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_USER");

			if (!String.IsNullOrWhiteSpace(userFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(userFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'DATAMINER_DEPLOY_USER_ENCRYPTED' with found token in Env Variable: 'DATAMINER_DEPLOY_USER'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_DEPLOY_USER'.");
				}

				userFromEnv = userFromEnvironment;
			}

			string pwFromEnvironment = Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD");

			if (!String.IsNullOrWhiteSpace(pwFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(pwFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'DATAMINER_DEPLOY_PASSWORD_ENCRYPTED' with found token in Env Variable: 'DATAMINER_DEPLOY_PASSWORD'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_DEPLOY_PASSWORD'.");
				}

				pwFromEnv = pwFromEnvironment;
			}
		}
	}
}