namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy
{
	using System.CommandLine;
	using System.Threading.Tasks;

	/// <summary>
	/// Deploys a package to DataMiner from the catalog or directly..
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
			name: "--artifactId",
			description: "The unique cloud artifact identifier as returned from performing a catalog-upload.")
			{
				IsRequired = true
			};

			var dmCatalogToken = new Option<string>(
			name: "--dmCatalogToken",
			description: "The key to deploy to a specific cloud-connected DataMiner as defined in admin.dataminer.services. This is optional if the key can also be provided using the 'dmcatalogtoken' environment variable (unix/win) or using 'dmcatalogtoken_encrypted' configured with Skyline.DataMiner.CICD.Tools.WinEncryptedKeys (windows).")
			{
				IsRequired = false
			};

			var FromCatalog = new Command("FromCatalogArtifactId", "Deploys a specific package from the cloud to a cloud-connected DataMiner agent.")
			{
				isDebug,
				artifactId,
				dmCatalogToken
			};


			var pathToArtifact = new Option<string>(
				name: "--pathToArtifact",
				description: "The path to a .dmapp or .dmprotocol file.")
			{
				IsRequired = true
			};

			var dataMinerServerLocation = new Option<string>(
			name: "--dmServerLocation",
			description: "The IP or host name of a DataMiner agent.")
			{
				IsRequired = true
			};

			var dataminerUser = new Option<string>(
			name: "--dmUser",
			description: "The dataminer User to setup a direct connection to an accessible agent.")
			{
				IsRequired = true
			};

			var dataminerPassword = new Option<string>(
			name: "--dmPassword",
			description: "The password to setup a direct connection to an accessible agent.")
			{
				IsRequired = true
			};

			var FromArtifact = new Command("FromArtifact", "Deploys a specific package from a local .dmapp to a DataMiner agent.")
			{
				isDebug,
				pathToArtifact,
				dataMinerServerLocation,
				dataminerUser,
				dataminerPassword
			};

			// Optionally can add add extra subcommands later to deploy from different locations to DataMiner.

			rootCommand.Add(FromArtifact);
			rootCommand.Add(FromCatalog);


			FromCatalog.SetHandler(ProcessCatalog, isDebug, artifactId, dmCatalogToken);
			FromArtifact.SetHandler(ProcessArtifact, isDebug, pathToArtifact, dataMinerServerLocation, dataminerUser, dataminerPassword);

			// dataminer-package-deploy
			await rootCommand.InvokeAsync(args);

			return 0;
		}

		private static async Task ProcessCatalog(bool isDebug, string artifactId, string dmCatalogToken)
		{
			//Main Code for program here
		}

		private static async Task ProcessArtifact(bool isDebug, string pathToArtifact, string dataMinerServerLocation, string dataminerUser, string dataminerPassword)
		{
			//Main Code for program here
		}
	}
}