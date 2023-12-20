namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;

	internal class CatalogArtifact : IArtifact
	{
		private readonly ILogger _logger;
		private readonly string artifactIdentifier;
		private readonly CancellationTokenSource cancellationTokenSource;
		private readonly string catalogAgentToken;
		private readonly ICatalogService service;
		private string keyFromEnv;
		private bool disposedValue;

		public CatalogArtifact(ICatalogService service, string artifactIdentifier, string catalogAgentToken, ILogger logger)
		{
			this.artifactIdentifier = artifactIdentifier;
			this.catalogAgentToken = catalogAgentToken;
			this._logger = logger;
			this.service = service;
			cancellationTokenSource = new CancellationTokenSource();
		}

		public CatalogArtifact(string artifactIdentifier, string catalogAgentToken, ILogger logger) : this(CatalogServiceFactory.CreateWithHttp(new System.Net.Http.HttpClient(), logger), artifactIdentifier, catalogAgentToken, logger)
		{
		}

		public CatalogArtifact(ICatalogService service, string artifactIdentifier, ILogger logger)
		{
			cancellationTokenSource = new CancellationTokenSource();
			this.artifactIdentifier = artifactIdentifier;
			this._logger = logger;
			this.service = service;
			TryFindEnvironmentKey();
			if (String.IsNullOrWhiteSpace(keyFromEnv))
			{
				throw new InvalidOperationException("Deployment failed, missing token in environment variable DATAMINER_CATALOG_TOKEN or DATAMINER_CATALOG_TOKEN_ENCRYPTED. Either add that, or use the provided catalogAgentToken argument.");
			}

			catalogAgentToken = keyFromEnv;
		}

		public CatalogArtifact(string artifactIdentifier, ILogger logger) : this(CatalogServiceFactory.CreateWithHttp(new System.Net.Http.HttpClient(), logger), artifactIdentifier, logger)
		{
		}

		/// <summary>
		/// Cancels an ongoing deployment. Create a new CatalogArtifact to attempt a new upload.
		/// </summary>
		public void CancelDeployment()
		{
			_logger.LogDebug($"Upload cancellation requested for {artifactIdentifier}");
			cancellationTokenSource.Cancel();
		}

		public async Task<bool> DeployAsync(TimeSpan timeout)
		{
			var deploying = await service.DeployPackageAsync(artifactIdentifier, catalogAgentToken, cancellationTokenSource.Token);
			if (deploying == null)
			{
				throw new InvalidOperationException($"Start Deployment of {artifactIdentifier} failed. CatalogService returned a null");
			}

			_logger.LogDebug("Deployment Started...");
			var output = await ConfirmSuccesfullDeploymentAsync(catalogAgentToken, TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(2), timeout, deploying);
			_logger.LogDebug("Deployment Finished.");
			_logger.LogInformation(JsonConvert.SerializeObject(output));

			return output.Status.Equals("succeeded", StringComparison.InvariantCultureIgnoreCase);
		}

		private async Task<DeployedPackage> ConfirmSuccesfullDeploymentAsync(string key, TimeSpan deploymentBackOff, TimeSpan deploymentMaxBackOff,
			TimeSpan deploymentTimeout, DeployingPackage deployingPackage)
		{
			DeployedPackage deployedPackage;
			var maxTimeout = deploymentTimeout.TotalSeconds;
			_logger.LogDebug($"Waiting {((int)deploymentBackOff.TotalSeconds)}s before checking deployment status.");
			await Task.Delay((int)deploymentBackOff.TotalMilliseconds);

			try
			{
				deployedPackage = await Utils.ExecuteWithRetryAsync(
					async () =>
					{
						if (cancellationTokenSource.IsCancellationRequested)
						{
							throw new OperationCanceledException();
						}

						return await service.GetDeployedPackageAsync(deployingPackage, key);
					},
					(output) => (output?.Status ?? "").Equals("succeeded", StringComparison.InvariantCultureIgnoreCase) || (output?.Status ?? "").Equals("Timeout", StringComparison.InvariantCultureIgnoreCase) || (output?.Status ?? "").Equals("Error", StringComparison.InvariantCultureIgnoreCase),
					(backOffDelaySeconds) => _logger.LogDebug($"Waiting {backOffDelaySeconds}s before checking next deployment status."),
					deploymentBackOff,
					deploymentMaxBackOff,
					deploymentTimeout.Subtract(deploymentBackOff));
			}
			catch (TimeoutException ex)
			{
				throw new TimeoutException($"Deployment Status was never succeeded, error or timeout after trying for {maxTimeout} seconds");
			}

			if (deployedPackage.Status.Equals("Timeout", StringComparison.InvariantCultureIgnoreCase) || deployedPackage.Status.Equals("Error", StringComparison.InvariantCultureIgnoreCase))
			{
				_logger.LogCritical($"Deployment Failed with status {deployedPackage.Status} for artifact {deployingPackage.ArtifactId}");
			}

			return deployedPackage;
		}

		/// <summary>
		///  Attempts to find the necessary API key in Environment Variables. In order of priority:
		///  <para>- key stored as an Environment Variable called "DATAMINER_CATALOG_TOKEN". (unix/win)</para>
		///  <para>- key configured using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys called "DATAMINER_CATALOG_TOKEN_ENCRYPTED" (windows only)</para>
		/// </summary>
		private void TryFindEnvironmentKey()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try
				{
					var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey("DATAMINER_CATALOG_TOKEN_ENCRYPTED");
					if (encryptedKey != null)
					{
						string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(String.Empty, encryptedKey).Password;

						if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
						{
							_logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_CATALOG_TOKEN_ENCRYPTED' created by WinEncryptedKeys.");
							keyFromEnv = keyFromWinEncryptedKeys;

							// Do not return. keyFromEnv Can be overwritten by the presence of DATAMINER_CATALOG_TOKEN
						}
					}
				}
				catch (InvalidOperationException)
				{
					// Gobble up, no key means we try the next thing.
				}
			}

			string keyFromEnvironment = Environment.GetEnvironmentVariable("DATAMINER_CATALOG_TOKEN");

			if (!String.IsNullOrWhiteSpace(keyFromEnvironment))
			{
				if (!String.IsNullOrWhiteSpace(keyFromEnv))
				{
					_logger.LogDebug("OK: Overriding 'DATAMINER_CATALOG_TOKEN_ENCRYPTED' with found token in Env Variable: 'DATAMINER_CATALOG_TOKEN'.");
				}
				else
				{
					_logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_CATALOG_TOKEN'.");
				}

				keyFromEnv = keyFromEnvironment;
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					cancellationTokenSource.Dispose();
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}