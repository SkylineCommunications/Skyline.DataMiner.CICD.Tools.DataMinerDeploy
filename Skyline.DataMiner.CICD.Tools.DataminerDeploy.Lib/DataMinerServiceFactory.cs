namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
	using Microsoft.Extensions.Logging;

	using Skyline.DataMiner.CICD.FileSystem;

	/// <summary>
	/// Factory that creates instances of <see cref="IDataMinerService"/> to communicate directly with a DataMiner agent.
	/// </summary>
	public static class DataMinerServiceFactory
	{
		/// <summary>
		/// Create a DataMinerService that uses SLNet for communication.
		/// </summary>
		/// <param name="fs">An instance of <see cref="IFileSystem"/> for use in reading files and folders.</param>
		/// <param name="logger">An instance of <see cref="ILogger"/> for logging and debugging purposes.</param>
		/// <returns>An instance of <see cref="IDataMinerService"/> that uses SLNet in the background.</returns>
		public static IDataMinerService CreateWithSLNet(IFileSystem fs, ILogger logger)
		{
			return new SLNetDataMinerService(fs, logger);
		}
	}
}