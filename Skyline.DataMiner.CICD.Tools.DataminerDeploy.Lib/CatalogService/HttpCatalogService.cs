namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Http;
	using System.Threading;
	using System.Threading.Tasks;

	using ArtifactDeploymentInfoApi.Generated;
	using ArtifactDeploymentInfoApi.Generated.Models;

	using DeployArtifactApi.Generated;
	using DeployArtifactApi.Generated.Models;

	using Microsoft.Extensions.Logging;
	using Microsoft.Rest;

	internal sealed class HttpCatalogService : ICatalogService, IDisposable
	{
		private const string DeploymentInfoKey = "DeploymentInfo";
		private readonly IArtifactDeploymentInfoAPI _artifactDeploymentInfoApi;
		private readonly DeployArtifactAPI _deployArtifactApi;
		private readonly ILogger _logger;

		public HttpCatalogService(HttpClient httpClient, ILogger logger)
		{
			_logger = logger;
			_deployArtifactApi = new DeployArtifactAPI(new BasicAuthenticationCredentials(), httpClient, false);
			_artifactDeploymentInfoApi = new ArtifactDeploymentInfoAPI(new BasicAuthenticationCredentials(), httpClient, false);

			// Need to override this, constructor of generated code uses a localhost address otherwise.
			_deployArtifactApi.BaseUri = httpClient.BaseAddress;
			_artifactDeploymentInfoApi.BaseUri = httpClient.BaseAddress;
		}

		/// <summary>
		/// Starts the deployment process on a DataMiner Server. You'll need to call
		/// </summary>
		/// <param name="artifactIdentifier"></param>
		/// <param name="key"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		public async Task<DeployingPackage> DeployPackageAsync(string artifactIdentifier, string key, CancellationToken cancellationToken)
		{
			HttpOperationResponse<DeploymentModel> res;

			try
			{
				_logger.LogDebug($"Deploying {artifactIdentifier}");
				res = await _deployArtifactApi.DeployArtifactWithApiKeyFunctionWithHttpMessagesAsync(new DeployArtifactAsSystemForm(artifactIdentifier), key, null, cancellationToken);
			}
			catch (HttpOperationException e)
			{
				throw new InvalidOperationException($"Azure Artifact Deploy failed with message {e.Response.Content}", e);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Couldn't deploy the package {e}", e);
			}

			if (res.Response.IsSuccessStatusCode)
			{
				if (Guid.TryParse(res.Body.DeploymentId, out var deploymentId))
				{
					_logger.LogDebug($"Deployment {deploymentId} started...");
					return new DeployingPackage(artifactIdentifier, deploymentId);
				}

				throw new InvalidOperationException("Received an invalid deployment ID");
			}

			if (res.Response.StatusCode is HttpStatusCode.Forbidden || res.Response.StatusCode is HttpStatusCode.Unauthorized)
			{
				throw new UnauthorizedAccessException($"The deploy API returned a response with status code {res.Response.StatusCode}");
			}

			var responseContent = String.Empty;
			if (res.Response.Content != null)
			{
				responseContent = await res.Response.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			throw new InvalidOperationException($"The deploy API returned a response with status code {res.Response.StatusCode}, content: {responseContent}");
		}

		public void Dispose()
		{
			_artifactDeploymentInfoApi.Dispose();
			_deployArtifactApi.Dispose();
		}

		public async Task<DeployedPackage> GetDeployedPackageAsync(DeployingPackage deployingPackage, string key)
		{
			HttpOperationResponse<IDictionary<string, DeploymentInfoModel>> res;

			try
			{
				_logger.LogDebug($"Checking Deployment Status...");
				res = await _artifactDeploymentInfoApi.GetPrivateArtifactDeploymentInfoWithHttpMessagesAsync(deployingPackage.DeploymentId, key);
			}
			catch (HttpOperationException e)
			{
				throw new InvalidOperationException($"Azure Artifact Deploy Check Status failed with message {e.Response.Content}", e);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Couldn't get the deployed package {e.ToString()}", e);
			}

			if (res.Response.IsSuccessStatusCode)
			{
				if (res.Body.TryGetValue(DeploymentInfoKey, out var deploymentInfoModel))
				{
					return new DeployedPackage(deploymentInfoModel.CurrentState);
				}

				throw new InvalidOperationException("Received an invalid deployment info response");
			}

			if (res.Response.StatusCode is HttpStatusCode.Forbidden || res.Response.StatusCode is HttpStatusCode.Unauthorized)
			{
				throw new InvalidOperationException($"The GetDeployedPackage API returned a response with status code {res.Response.StatusCode}");
			}

			var responseContent = String.Empty;
			if (res.Response.Content != null)
			{
				responseContent = await res.Response.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			throw new InvalidOperationException($"The GetDeployedPackage API returned a response with status code {res.Response.StatusCode}, content: {responseContent}");
		}
	}
}