namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Runtime.InteropServices;
	using System.Threading.Tasks;
	using Skyline.DataMiner.CICD.FileSystem;

	using Microsoft.Extensions.Logging;
	using System.IO.Compression;
	using System.IO;
	using System.Net.Mime;
	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerArtifacts;

	internal class LocalArtifact : IArtifact
	{
		private readonly ILogger _logger;
		private readonly string dataminerPassword;
		private readonly string dataMinerServerLocation;
		private readonly string dataminerUser;
		private readonly string pathToArtifact;
		private string userFromEnv;
		private string pwFromEnv;
		private readonly IDataMinerService service;
		private readonly IFileSystem fs;

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

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, string dataMinerUser, string dataMinerPassword, ILogger logger, IFileSystem fs) : this(new SLNetDataMinerService(fs, logger), pathToArtifact, dataMinerServerLocation, dataMinerUser, dataMinerPassword, logger, fs)
		{
		}

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, ILogger logger, IFileSystem fs) : this(pathToArtifact, dataMinerServerLocation, null, null, logger, fs)
		{
		}

		public void CancelDeployment()
		{
			throw new NotImplementedException();
		}

		public async Task<bool> DeployAsync(TimeSpan timeout)
		{
			if (!fs.File.Exists(pathToArtifact))
			{
				throw new InvalidOperationException($"Unable to deploy, path does not exist: {pathToArtifact}");
			}

			var actualUser = dataminerUser ?? userFromEnv;
			var actualPassword = dataminerPassword ?? userFromEnv;
			service.TryConnect(dataMinerServerLocation, actualUser, actualPassword);
			ArtifactType type = new ArtifactType(pathToArtifact);

			switch (type.Value)
			{
				case ArtifactTypeEnum.dmapp:
					service.InstallNewStyleAppPackages(pathToArtifact);
					break;
				case ArtifactTypeEnum.legacyDmapp:
					service.InstallOldStyleAppPackages(pathToArtifact);
					break;
				case ArtifactTypeEnum.dmprotocol:
					service.InstallDataminerProtocol(pathToArtifact);
					break;
				default:
					break;
			}

			return true;
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
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("dmusername_encrypted");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(string.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'dmusername_encrypted' created by WinEncryptedKeys.");
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
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("dmpassword_encrypted");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(string.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'dmpassword_encrypted' created by WinEncryptedKeys.");
							pwFromEnv = keyFromWinEncryptedKeys;
						}
					}
				}
				catch (InvalidOperationException)
				{
					// Gobble up, no key means we try the next thing.
				}
			}

			string userFromEnvironment = Environment.GetEnvironmentVariable("dmusername");

			if (!String.IsNullOrWhiteSpace(userFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(userFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'dmusername_encrypted' with found token in Env Variable: 'dmusername'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'dmusername'.");
				}

				userFromEnv = userFromEnvironment;
			}

			string pwFromEnvironment = Environment.GetEnvironmentVariable("dmpassword");

			if (!String.IsNullOrWhiteSpace(pwFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(pwFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'dmpassword_encrypted' with found token in Env Variable: 'dmpassword'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'dmpassword'.");
				}

				pwFromEnv = pwFromEnvironment;
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
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("dmusername_encrypted");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(string.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'dmusername_encrypted' created by WinEncryptedKeys.");
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
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("dmpassword_encrypted");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(string.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'dmpassword_encrypted' created by WinEncryptedKeys.");
							pwFromEnv = keyFromWinEncryptedKeys;
						}
					}
				}
				catch (InvalidOperationException)
				{
					// Gobble up, no key means we try the next thing.
				}
			}

			string userFromEnvironment = Environment.GetEnvironmentVariable("dmusername");

			if (!String.IsNullOrWhiteSpace(userFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(userFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'dmusername_encrypted' with found token in Env Variable: 'dmusername'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'dmusername'.");
				}

				userFromEnv = userFromEnvironment;
			}

			string pwFromEnvironment = Environment.GetEnvironmentVariable("dmpassword");

			if (!String.IsNullOrWhiteSpace(pwFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(pwFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'dmpassword_encrypted' with found token in Env Variable: 'dmpassword'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'dmpassword'.");
				}

				pwFromEnv = pwFromEnvironment;
			}
		}
	}
}