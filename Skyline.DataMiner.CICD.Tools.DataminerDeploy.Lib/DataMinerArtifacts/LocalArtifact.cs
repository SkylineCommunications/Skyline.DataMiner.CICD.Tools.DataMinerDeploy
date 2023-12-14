namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Runtime.InteropServices;
	using System.Threading.Tasks;

	using Microsoft.Extensions.Logging;

	internal class LocalArtifact : IArtifact
	{
		private readonly ILogger _logger;
		private readonly string dataminerPassword;
		private readonly string dataMinerServerLocation;
		private readonly string dataminerUser;
		private readonly string pathToArtifact;
		private string userFromEnv;
		private string pwFromEnv;
		IDataMinerService service;


		public LocalArtifact(IDataMinerService dataminerService, string pathToArtifact, string dataMinerServerLocation, string dataminerUser, string dataminerPassword, ILogger logger)
		{
			this.service = dataminerService;
			this.pathToArtifact = pathToArtifact;
			this.dataMinerServerLocation = dataMinerServerLocation;
			this.dataminerUser = dataminerUser;
			this.dataminerPassword = dataminerPassword;
			this._logger = logger;
			TryFindEnvironmentKeys();
		}

		public LocalArtifact(IDataMinerService dataMinerService, string pathToArtifact, string dataMinerServerLocation, ILogger logger) : this(dataMinerService, pathToArtifact, dataMinerServerLocation, null, null, logger)
		{
		}

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, string dataminerUser, string dataminerPassword, ILogger logger) : this(new SLNetDataMinerService("TODO"), pathToArtifact, dataMinerServerLocation, dataminerUser, dataminerPassword, logger)
		{
		}

		public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, ILogger logger) : this(pathToArtifact, dataMinerServerLocation, null, null, logger)
		{
		}

		public void CancelDeployment()
		{
			throw new NotImplementedException();
		}

		public async Task<bool> DeployAsync(TimeSpan timeout)
		{
			var actualUser = dataminerUser ?? userFromEnv;
			var actualPassword = dataminerPassword ?? userFromEnv;
			service.TryConnect(dataMinerServerLocation, actualUser, actualPassword);

			// TODO Need to check if the package is legacy or not
			bool newStyle = true;

			if (newStyle)
			{
				service.InstallNewStyleAppPackages(pathToArtifact);
			}
			else
			{
				service.InstallOldStyleAppPackages(pathToArtifact);
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