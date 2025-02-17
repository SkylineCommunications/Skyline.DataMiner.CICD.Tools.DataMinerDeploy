namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy
{
    using System;
    using System.CommandLine;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using DotnetActionsToolkit;

    using Microsoft.Extensions.Logging;

    using Serilog;

    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib;
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.CatalogService;
    using Skyline.DataMiner.CICD.Tools.Reporter;

    /// <summary>
    /// Deploys a package to DataMiner from the catalog or directly.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Code that will be called when running the tool.
        /// </summary>
        /// <param name="args">Extra arguments.</param>
        /// <returns>0 if successful.</returns>
        public static async Task<int> Main(string[] args)
        {
            var isDebug = new Option<bool>(
            name: "--debug",
            description: "Indicates the tool should write out debug logging.")
            {
                IsRequired = false,
            };

            var rootCommand = new RootCommand("Deploys an artifact to DataMiner.")
            {
                isDebug,
            };

            rootCommand.AddGlobalOption(isDebug);

            var artifactId = new Option<string>(
            name: "--artifact-id",
            description: "The unique catalog artifact identifier. This is the catalog GUID of the item.")
            {
                IsRequired = true
            };

            var artifactVersion = new Option<string>(
            name: "--artifact-version",
            description: "The version of the catalog item you want to deploy.")
            {
                // Too many people using from-catalog as it currently exists. Cannot make this mandatory for now.
                IsRequired = false
            };

            var agentDestinationId = new Option<string>(
            name: "--agent-destination-id",
            description: "The destination agent ID to deploy to. To obtain this ID for an existing DataMiner System, navigate to its details page in the Admin app. The ID is the last GUID of the URL. This is required when the dm-catalog-token is an Organization Token.")
            {
                // Too many people using from-catalog as it currently exists. Cannot make this mandatory for now.
                IsRequired = false
            };

            var dmCatalogToken = new Option<string>(
            name: "--dm-catalog-token",
            description: "The key to deploy to a specific cloud-connected DataMiner as defined in admin.dataminer.services. This is optional if the key can also be provided using the 'DATAMINER_CATALOG_TOKEN' environment variable (Unix/Windows) or using 'DATAMINER_CATALOG_TOKEN_ENCRYPTED' configured with Skyline.DataMiner.CICD.Tools.WinEncryptedKeys (Windows).")
            {
                IsRequired = false
            };

            var deployTimeout = new Option<int>(
            name: "--deploy-timeout-in-seconds",
            description: "Timeout in seconds to wait on successful deployment. This is optional, if not provided this will be 15 minutes. Fill in 0 for infinite.")
            {
                IsRequired = false,
            };

            deployTimeout.SetDefaultValue(900);

            var fromCatalog = new Command("from-catalog", "Deploys a specific package from the cloud to a cloud-connected DataMiner Agent. Currently only supports private artifacts uploaded using a key from the organization.")
            {
                isDebug,
                artifactId,
                artifactVersion,
                agentDestinationId,
                dmCatalogToken,
                deployTimeout
            };

            var pathToArtifact = new Option<string>(
                name: "--path-to-artifact",
                description: "Path to the application package (.dmapp) or protocol package (.dmprotocol).")
            {
                IsRequired = true,
            };

            var dataMinerServerLocation = new Option<string>(
            name: "--dm-server-location",
            description: "The IP address or host name of the DataMiner Agent.")
            {
                IsRequired = true
            };

            var dataminerUser = new Option<string>(
            name: "--dm-user",
            description: "The DataMiner user to set up a direct connection to an accessible Agent. This is optional if the key can also be provided using the 'DATAMINER_DEPLOY_USER' environment variable (Unix/Windows) or using 'DATAMINER_DEPLOY_USER_ENCRYPTED' configured with Skyline.DataMiner.CICD.Tools.WinEncryptedKeys (Windows).")
            {
                IsRequired = false
            };

            var dataminerPassword = new Option<string>(
            name: "--dm-password",
            description: "The password to set up a direct connection to an accessible Agent. This is optional if the key can also be provided using the 'DATAMINER_DEPLOY_PASSWORD' environment variable (Unix/Windows) or using 'DATAMINER_DEPLOY_PASSWORD_ENCRYPTED' configured with Skyline.DataMiner.CICD.Tools.WinEncryptedKeys (Windows).")
            {
                IsRequired = false
            };

            var postAction = new Option<PostActionsInputArgument>("--post-action")
            {
                Description = "Specify the post-action to perform. This only works for protocol packages (.dmprotocol).",
                IsRequired = false
            };

            postAction.SetDefaultValue(PostActionsInputArgument.None);

            var fromArtifact = new Command("from-artifact", "Deploys a specific package from a local application package (.dmapp) or protocol package (.dmprotocol) to a DataMiner Agent. Warning: if using legacy application packages (.dmapp that you unzip, contains an Update.zip) the remote Agent will perform a restart.")
            {
                isDebug,
                pathToArtifact,
                dataMinerServerLocation,
                dataminerUser,
                dataminerPassword,
                deployTimeout,
                postAction
            };

            // Optionally can add extra subcommands later to deploy from different sources to DataMiner.
            rootCommand.Add(fromArtifact);
            rootCommand.Add(fromCatalog);

            fromCatalog.SetHandler(ProcessCatalog, isDebug, artifactId, artifactVersion, agentDestinationId, dmCatalogToken, deployTimeout);
            fromArtifact.SetHandler(ProcessArtifact, isDebug, pathToArtifact, dataMinerServerLocation, dataminerUser, dataminerPassword, deployTimeout, postAction);

            // dataminer-package-deploy
            int value = await rootCommand.InvokeAsync(args);
            return value;
        }

        /// <summary>
        /// Extracts the artifact ID from a common output received from the dataminer-catalog-upload tool.
        /// </summary>
        /// <param name="artifactId">The input could include a json or other debug info.</param>
        /// <returns>The extracted clean artifactId.</returns>
        internal static (string id, string version) ExtractArtifactId(string artifactId)
        {
            // smart filtering of input artifactId
            // could have extra debug info as well: "[11:41:24 INF] {artifactId:dmscript/bcbe888f-36aa-4f60-8e12-61fe0bc9d22b}"}

            string cleanArtifactId = artifactId;
            string cleanArtifactVersion = String.Empty;

            int lastInformation = artifactId.LastIndexOf("INF]", StringComparison.Ordinal);
            if (lastInformation == -1)
            {
                return (cleanArtifactId, cleanArtifactVersion);
            }

            string onlyTheJson = artifactId.Substring(lastInformation + 4);
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(onlyTheJson))
                {
                    JsonElement root = doc.RootElement;
                    var jsonIdProperty = root.GetProperty("artifactId");
                    var jsonVersionProperty = root.GetProperty("artifactVersion");
                    cleanArtifactId = jsonIdProperty.GetString();
                    cleanArtifactVersion = jsonVersionProperty.GetString();
                }
            }
            catch
            {
                // Best effort. Gobble up parsing exceptions and try to handle situations with weird escapings like PowerShell
                var idStart = onlyTheJson.IndexOf("artifactId", StringComparison.Ordinal);
                if (idStart != -1)
                {
                    var idStop = onlyTheJson.IndexOf(",", idStart, StringComparison.Ordinal);
                    if (idStop == -1) idStop = onlyTheJson.IndexOf("}", idStart, StringComparison.Ordinal);
                    if (idStop != -1)
                    {
                        var jsonIdProperty = onlyTheJson.Substring(idStart + 11, idStop - 11 - idStart);

                        Regex regex = new Regex(@"[\s,:.;\\""']+");
                        cleanArtifactId = regex.Replace(jsonIdProperty, "");
                    }
                }

                var versionStart = onlyTheJson.IndexOf("artifactVersion", StringComparison.Ordinal);
                if (versionStart != -1)
                {
                    var versionStop = onlyTheJson.IndexOf(",", versionStart, StringComparison.Ordinal);
                    if (versionStop == -1) versionStop = onlyTheJson.IndexOf("}", versionStart, StringComparison.Ordinal);
                    if (versionStop != -1)
                    {
                        var jsonVersionProperty = onlyTheJson.Substring(versionStart + 16, versionStop - 16 - versionStart);

                        Regex regex = new Regex(@"[\s,:;\\""']+");
                        cleanArtifactVersion = regex.Replace(jsonVersionProperty, "");
                    }
                }
            }

            return (cleanArtifactId, cleanArtifactVersion);
        }

        private static async Task<int> ProcessArtifact(bool isDebug, string pathToArtifact, string dataMinerServerLocation, string dataminerUser, string dataminerPassword, int deployTimeout, PostActionsInputArgument actions)
        {
            // Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-artifact|Status:OK"
            // Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-artifact|Status:Fail-blabla"
            string devopsMetricsMessage = $"Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-artifact";

            LoggerConfiguration logConfig = new LoggerConfiguration().WriteTo.Console();

            if (!isDebug)
            {
                logConfig.MinimumLevel.Information();
            }
            else
            {
                logConfig.MinimumLevel.Debug();
            }

            var seriLog = logConfig.CreateLogger();

            LoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog(seriLog);

            var logger = loggerFactory.CreateLogger("Skyline.DataMiner.CICD.Tools.DataMinerDeploy");
            try
            {
                IArtifact artifact;

                if (String.IsNullOrWhiteSpace(dataminerPassword))
                {
                    artifact = DeploymentFactory.Local(FileSystem.FileSystem.Instance, pathToArtifact, logger, dataMinerServerLocation);
                }
                else
                {
                    artifact = DeploymentFactory.Local(FileSystem.FileSystem.Instance, pathToArtifact, logger, dataMinerServerLocation, dataminerUser, dataminerPassword);
                }
                try
                {
                    if (actions != PostActionsInputArgument.None)
                    {
                        PostDeployActions postDeployActions = new PostDeployActions();

                        switch (actions)
                        {
                            case PostActionsInputArgument.SetToProduction:
                                postDeployActions.SetToProduction = (true, false);
                                break;

                            case PostActionsInputArgument.SetToProductionIncludingTemplates:
                                postDeployActions.SetToProduction = (true, true);
                                break;

                            default:
                                break;
                        }

                        artifact.AddPostDeployActions(postDeployActions);
                    }

                    if (deployTimeout < 0)
                    {
                        deployTimeout = 900; // Default to 15min
                    }
                    else if (deployTimeout == 0)
                    {
                        deployTimeout = Int32.MaxValue; // MaxValue
                    }

                    if (await artifact.DeployAsync(TimeSpan.FromSeconds(deployTimeout)))
                    {
                        devopsMetricsMessage += "|Status:OK";
                        return 0;
                    }
                    else
                    {
                        devopsMetricsMessage += "|Status:Fail-Deployment returned false";
                        logger.LogCritical("Fail-Deployment returned false");
                        return 1;
                    }
                }
                finally
                {
                    artifact.Dispose();
                }
            }
            catch (Exception ex)
            {
                devopsMetricsMessage += "|Status:Fail-" + ex.Message;
                logger.LogCritical(ex.ToString());
                return 1;
            }
            finally
            {
                if (!String.IsNullOrWhiteSpace(devopsMetricsMessage))
                {
                    try
                    {
                        DevOpsMetrics devOpsMetrics = new DevOpsMetrics();
                        await devOpsMetrics.ReportAsync(devopsMetricsMessage);
                    }
                    catch
                    {
                        // Fire and forget.
                    }
                }
            }
        }

        private static async Task<int> ProcessCatalog(bool isDebug, string artifactId, string artifactVersion, string agentDestinationId, string dmCatalogToken, int deployTimeout)
        {
            // Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-catalog:aaz4s555e74a55z7e4|Status:OK"
            // Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-catalog:aaz4s555e74a55z7e4|Status:Fail-blabla"
            string devopsMetricsMessage = $"Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-catalog:{artifactId}";

            var artifactExtrated = ExtractArtifactId(artifactId);
            artifactId = artifactExtrated.id;

            if (String.IsNullOrWhiteSpace(artifactVersion)) artifactVersion = artifactExtrated.id;

            LoggerConfiguration logConfig = new LoggerConfiguration().WriteTo.Console();
            if (!isDebug)
            {
                logConfig.MinimumLevel.Information();
            }
            else
            {
                logConfig.MinimumLevel.Debug();
            }

            var seriLog = logConfig.CreateLogger();

            LoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog(seriLog);

            var logger = loggerFactory.CreateLogger("Skyline.DataMiner.CICD.Tools.DataMinerDeploy");

            try
            {
                IArtifact artifact;

                if (String.IsNullOrWhiteSpace(dmCatalogToken))
                {
                    if (String.IsNullOrWhiteSpace(agentDestinationId))
                    {
                        DotnetActionsToolkit.Core core = new DotnetActionsToolkit.Core();
                        core.Warning("Subcommand: 'from-catalog --artifact-id {upload result}' using the Agent Key is deprecated and must be replaced by 'from-catalog --artifact-id {catalog guid} --artifact-version {catalog version} --agent-destination-id {agent guid}' and using the Organization key instead. This change will be pushed in a new major change of this tool on 01/05/2025.");
                        artifact = DeploymentFactory.Cloud(artifactId, logger);
                    }
                    else
                    {
                        KeyCatalogDeploymentIdentifier catId = new KeyCatalogDeploymentIdentifier()
                        {
                            CatalogGuid = artifactId,
                            CatalogVersion = artifactVersion,
                            DestinationGuid = agentDestinationId,
                        };

                        artifact = DeploymentFactory.Catalog(catId, logger);
                    }
                }
                else
                {
                    if (String.IsNullOrWhiteSpace(agentDestinationId))
                    {
                        DotnetActionsToolkit.Core core = new DotnetActionsToolkit.Core();
                        core.Warning("Subcommand: 'from-catalog --artifact-id {upload result}' using the Agent Key is deprecated and must be replaced by 'from-catalog --artifact-id {catalog guid} --artifact-version {catalog version} --agent-destination-id {agent guid}' and using the Organization key instead. This change will be pushed in a new major change of this tool on 01/05/2025.");
                        artifact = DeploymentFactory.Cloud(artifactId, dmCatalogToken, logger);
                    }
                    else
                    {
                        KeyCatalogDeploymentIdentifier catId = new KeyCatalogDeploymentIdentifier()
                        {
                            CatalogGuid = artifactId,
                            CatalogVersion = artifactVersion,
                            DestinationGuid = agentDestinationId,
                        };

                        artifact = DeploymentFactory.Catalog(catId, dmCatalogToken, logger);
                    }
                }

                try
                {
                    if (deployTimeout < 0)
                    {
                        deployTimeout = 900; // Default to 15min
                    }
                    else if (deployTimeout == 0)
                    {
                        deployTimeout = Int32.MaxValue; // MaxValue
                    }

                    if (await artifact.DeployAsync(TimeSpan.FromSeconds(deployTimeout)))
                    {
                        devopsMetricsMessage += "|Status:OK";
                        return 0;
                    }
                    else
                    {
                        devopsMetricsMessage += "|Status:Fail-Deployment returned false";
                        logger.LogCritical("Fail-Deployment returned false");
                        return 1;
                    }
                }
                finally
                {
                    artifact.Dispose();
                }
            }
            catch (Exception ex)
            {
                devopsMetricsMessage += "|Status:Fail-" + ex.Message;
                logger.LogCritical(ex.ToString());
                return 1;
            }
            finally
            {
                if (!String.IsNullOrWhiteSpace(devopsMetricsMessage))
                {
                    try
                    {
                        DevOpsMetrics devOpsMetrics = new DevOpsMetrics();
                        await devOpsMetrics.ReportAsync(devopsMetricsMessage);
                    }
                    catch
                    {
                        // Fire and forget.
                    }
                }
            }
        }
    }
}
