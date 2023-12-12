namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Runtime.InteropServices;
	using System.Security.Authentication;
	using System.Threading;
	using System.Threading.Tasks;

	using ArtifactDeploymentInfoApi.Generated;
	using ArtifactDeploymentInfoApi.Generated.Models;

	using DeployArtifactApi.Generated;
	using DeployArtifactApi.Generated.Models;

	using Microsoft.Extensions.Logging;
	using Microsoft.Rest;

	using Newtonsoft.Json;

	internal sealed class HttpCatalogService : ICatalogService, IDisposable
	{
		private readonly HttpClient _httpClient;
		private readonly ILogger _logger;
		private readonly DeployArtifactAPI _deployArtifactApi;
		private readonly IArtifactDeploymentInfoAPI _artifactDeploymentInfoApi;
		private const string DeploymentInfoKey = "DeploymentInfo";

		public HttpCatalogService(HttpClient httpClient, ILogger logger)
		{
			_logger = logger;
			_httpClient = httpClient;
			_deployArtifactApi = new DeployArtifactAPI(new BasicAuthenticationCredentials(), httpClient, true);
		}

		/// <summary>
		/// Starts the deployment process on a DataMiner Server. You'll need to call 
		/// </summary>
		/// <param name="artifactIdentifier"></param>
		/// <param name="key"></param>
		/// <param name="cancellation"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="UnauthorizedAccessException"></exception>
		public async Task<DeployingPackage> DeployPackageAsync(string artifactIdentifier, string key, CancellationToken cancellation)
		{
			HttpOperationResponse<DeploymentModel> res;

			try
			{
				_logger.LogDebug($"Deploying {artifactIdentifier}");
				res = await _deployArtifactApi.DeployArtifactWithApiKeyFunctionWithHttpMessagesAsync(new DeployArtifactAsSystemForm(artifactIdentifier), key);
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

		public async Task<DeployedPackage> GetDeployedPackageAsync(DeployingPackage deployingPackage, string key)
		{
			HttpOperationResponse<IDictionary<string, DeploymentInfoModel>> res;

			try
			{
				_logger.LogDebug($"Checking Deployment Status...");
				res = await _artifactDeploymentInfoApi.GetPrivateArtifactDeploymentInfoWithHttpMessagesAsync(deployingPackage.DeploymentId, key);
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

			var responseContent = string.Empty;
			if (res.Response.Content != null)
			{
				responseContent = await res.Response.Content.ReadAsStringAsync().ConfigureAwait(false);
			}

			throw new InvalidOperationException($"The GetDeployedPackage API returned a response with status code {res.Response.StatusCode}, content: {responseContent}");
		}


		public void Dispose()
		{
			_deployArtifactApi.Dispose();
		}
	}
}