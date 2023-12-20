namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy
{
	using System;
	using System.CommandLine;
	using System.Threading.Tasks;

	using Microsoft.Extensions.Logging;

	using Serilog;

	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib;
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
			description: "The unique cloud artifact identifier as returned from performing a catalog-upload. e.g. dmscript/f764389f-5404-4c32-9ac9-b54366a3d5e0")
			{
				IsRequired = true
			};

			var dmCatalogToken = new Option<string>(
			name: "--dm-catalog-token",
			description: "The key to deploy to a specific cloud-connected DataMiner as defined in admin.dataminer.services. This is optional if the key can also be provided using the 'DATAMINER_CATALOG_TOKEN' environment variable (unix/win) or using 'DATAMINER_CATALOG_TOKEN_ENCRYPTED' configured with Skyline.DataMiner.CICD.Tools.WinEncryptedKeys (windows).")
			{
				IsRequired = false
			};

			var deployTimeout = new Option<int>(
			name: "--deploy-timeout-in-seconds",
			description: "Time-Out time in seconds to wait on successful deployment. This is optional, if not provided this will be 15 minutes. Fill in 0 for infinite.")
			{
				IsRequired = false,
			};

			deployTimeout.SetDefaultValue(-1);

			var fromCatalog = new Command("from-catalog", "Deploys a specific package from the cloud to a cloud-connected DataMiner agent. Currently only supports private artifacts uploaded using a key from the organization.")
			{
				isDebug,
				artifactId,
				dmCatalogToken,
				deployTimeout
			};

			// Optionally can add add extra subcommands later to deploy from different sources to DataMiner.
			rootCommand.Add(fromCatalog);

			fromCatalog.SetHandler(ProcessCatalog, isDebug, artifactId, dmCatalogToken, deployTimeout);

			// dataminer-package-deploy
			await rootCommand.InvokeAsync(args);

			return 0;
		}

		private static async Task ProcessCatalog(bool isDebug, string artifactId, string dmCatalogToken, int deployTimeout)
		{
			// Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-catalog:aaz4s555e74a55z7e4|Status:OK"
			// Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-catalog:aaz4s555e74a55z7e4|Status:Fail-blabla"
			string devopsMetricsMessage = $"Skyline.DataMiner.CICD.Tools.DataMinerDeploy|from-catalog:{artifactId}";

			try

			{
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

				IArtifact artifact;
				if (String.IsNullOrWhiteSpace(dmCatalogToken))
				{
					artifact = DeploymentFactory.Cloud(artifactId, logger);
				}
				else
				{
					artifact = DeploymentFactory.Cloud(artifactId, dmCatalogToken, logger);
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
				}
				else
				{
					devopsMetricsMessage += "|Status:Fail-Deployment returned false";
				}
			}
			catch (Exception ex)
			{
				devopsMetricsMessage += "|Status:Fail-" + ex.Message;
				throw;
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