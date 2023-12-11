namespace Skyline.DataMiner.CICD.Tools.DataminerDeploy.Lib
{
    using System;
    using System.Net.Http;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Creates instances of <see cref="ICatalogService"/> to communicate with the Skyline DataMiner Catalog (https://catalog.dataminer.services/).
    /// </summary>
    public static class CatalogServiceFactory
    {
        /// <summary>
        /// Creates instances of <see cref="ICatalogService"/> to communicate with the Skyline DataMiner Catalog (https://catalog.dataminer.services/) using HTTP for communication.
        /// </summary>
        /// <param name="httpClient">An instance of <see cref="HttpClient"/> used for communication with the catalog.</param>
        /// <param name="logger">An instance of <see cref="ILogger"/> for handling debug and error logging.</param>
        /// <returns>An instance of <see cref="ICatalogService"/> to communicate with the Skyline DataMiner Catalog (https://catalog.dataminer.services/).</returns>
        public static ICatalogService CreateWithHttp(HttpClient httpClient, ILogger logger)
        {
            var environment = Environment.GetEnvironmentVariable("Skyline-deploy-action-namespace");

            string apiBaseUrl;
            if (environment != null)
            {
                apiBaseUrl = $"https://api-{environment}.dataminer.services/{environment}";
                logger.LogDebug("Found the \"Skyline-deploy-action-namespace\" environment variable");
                logger.LogDebug("Setting the base url for the api to: {0}", apiBaseUrl);
            }
            else
            {
                apiBaseUrl = "https://api.dataminer.services";
            }

            httpClient.BaseAddress = new Uri($"{apiBaseUrl}/");
            return new HttpCatalogService(httpClient, logger);
        }
    }
}