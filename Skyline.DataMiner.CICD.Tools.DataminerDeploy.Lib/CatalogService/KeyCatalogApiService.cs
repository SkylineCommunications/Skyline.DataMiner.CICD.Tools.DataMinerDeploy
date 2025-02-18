namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;

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


        public KeyCatalogServiceApi(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
            string versionDeployPath = $"{DeploymentPathStart}{idInfo[0]}{DeploymentPathMid}{idInfo[1]}{DeploymentEnd}?coordinationId={idInfo[2]}";

            using (var request = new HttpRequestMessage(HttpMethod.Post, versionDeployPath))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

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
            try
            {
                // No known implementation to check with Organization Token.
                // ALWAYS RETURNS TRUE
                _logger.LogDebug($"Deployment Status: Unknown - Skip and return OK");
                return new DeployedPackage("succeeded");
            }
            catch (HttpOperationException e)
            {
                throw new InvalidOperationException($"Azure artifact deploy check status failed with message {e.Response.Content}", e);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Could not get the deployed package {e.ToString()}", e);
            }
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}