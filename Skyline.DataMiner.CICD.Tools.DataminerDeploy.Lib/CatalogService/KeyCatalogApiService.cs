namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.InteropServices.ComTypes;
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

    internal sealed class KeyCatalogServiceApi : ICatalogService, IDisposable
    {
        /// <summary>
        /// Artifact information returned from uploading an artifact to the catalog using the non-volatile upload.
        /// </summary>
        private sealed class CatalogDeployResult
        {
            [JsonProperty("deploymentId")]
            public string DeploymentId { get; set; }

        }

        private const string DeploymentPathStart = "api/key-catalog/v2-0/catalogs/";
        private const string DeploymentPathMid = "/versions/";
        private const string DeploymentEnd = "/deploy";
        private readonly HttpClient _httpClient;

        private readonly ILogger _logger;

        // Workaround: Old API needed to track the deployment status
        private readonly IArtifactDeploymentInfoAPI _artifactDeploymentInfoApi; 
        private const string DeploymentInfoKey = "DeploymentInfo";

        public KeyCatalogServiceApi(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _artifactDeploymentInfoApi = new ArtifactDeploymentInfoAPI(new BasicAuthenticationCredentials(), httpClient, false);
            _artifactDeploymentInfoApi.BaseUri = httpClient.BaseAddress;
        }

        /// <summary>
        /// Starts the deployment process on a DataMiner Server. You'll need to call GetDeployedPackageAsync to see if it's finished.
        /// </summary>
        /// <param name="artifactIdentifier">The guid of the artifact.</param>
        /// <param name="key">The token for accessing and deploying on an agent.</param>
        /// <param name="cancellationToken">A token to cancel the deployment.</param>
        /// <returns>An identifier of the deployment process.</returns>
        /// <exception cref="InvalidOperationException">When deployment fails.</exception>
        /// <exception cref="UnauthorizedAccessException">When authentication to the agent and azure fails.</exception>
        public async Task<DeployingPackage> DeployPackageAsync(string artifactIdentifier, string key, CancellationToken cancellationToken)
        {
            var idInfo = artifactIdentifier.Split('|');
            if (idInfo.Length != 3) throw new InvalidOperationException("Invalid ArtifactIdentifier, expected id|version|destination");

            //api/key-catalog/v2-0/catalogs/ID/versions/VERSION/deploy
            string versionUploadPath = $"{DeploymentPathStart}{idInfo[0]}{DeploymentPathMid}{idInfo[1]}{DeploymentEnd}";

            using (var formData = new MultipartFormDataContent())
            {
                formData.Headers.Add("Ocp-Apim-Subscription-Key", key);

                // Add version information to the form data
                formData.Add(new StringContent(idInfo[0]), "catalogId");
                formData.Add(new StringContent(idInfo[1]), "versionId");
                formData.Add(new StringContent(idInfo[2]), "coordinationId");

                // Make the HTTP POST request
                var response = await _httpClient.PostAsync(versionUploadPath, formData, cancellationToken).ConfigureAwait(false);

                // Read and log the response body
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var returnedResult = JsonConvert.DeserializeObject<CatalogDeployResult>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    return new DeployingPackage(artifactIdentifier, Guid.Parse(returnedResult.DeploymentId));
                }

                if (response.StatusCode is HttpStatusCode.Forbidden || response.StatusCode is HttpStatusCode.Unauthorized)
                {
                    throw new AuthenticationException($"The catalog deployment api returned a {response.StatusCode} response. Body: {body}");
                }

                throw new InvalidOperationException($"The catalog deployment api returned a {response.StatusCode} response. Body: {body}");

            }
        }

        public async Task<DeployedPackage> GetDeployedPackageAsync(DeployingPackage deployingPackage, string key)
        {
            HttpOperationResponse<IDictionary<string, DeploymentInfoModel>> res;

            try
            {
                _logger.LogDebug($"Checking deployment status...");
                res = await _artifactDeploymentInfoApi.GetPrivateArtifactDeploymentInfoWithHttpMessagesAsync(deployingPackage.DeploymentId, key);
            }
            catch (HttpOperationException e)
            {
                throw new InvalidOperationException($"Azure artifact deploy check status failed with message {e.Response.Content}", e);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Could not get the deployed package {e.ToString()}", e);
            }

            if (res.Response.IsSuccessStatusCode)
            {
                if (res.Body.TryGetValue(DeploymentInfoKey, out var deploymentInfoModel))
                {
                    return new DeployedPackage(deploymentInfoModel.CurrentState);
                }

                throw new InvalidOperationException("Received an invalid deployment info response.");
            }

            if (res.Response.StatusCode is HttpStatusCode.Forbidden || res.Response.StatusCode is HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException($"The GetDeployedPackage API returned a response with status code {res.Response.StatusCode}.");
            }

            var responseContent = String.Empty;
            if (res.Response.Content != null)
            {
                responseContent = await res.Response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new InvalidOperationException($"The GetDeployedPackage API returned a response with status code {res.Response.StatusCode}, content: {responseContent}");
        }

        public void Dispose()
        {
            _artifactDeploymentInfoApi.Dispose();
        }
    }
}