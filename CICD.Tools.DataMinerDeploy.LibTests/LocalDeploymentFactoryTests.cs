namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.Tests
{
	using System;

	using FluentAssertions;

	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Mock;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	using Moq;

	using Skyline.DataMiner.CICD.FileSystem;
	using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib;

	[TestClass()]
	public class DeploymentFactoryTests
	{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		private Mock<ILogger> fakeLogger;
		private ILogger logger;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		private string originalUser_encrypt;
		private string originalUser;
		private string originalPw_encrypt;
		private string originalPw;

		[TestInitialize()]
		public void Initialize()
		{
			fakeLogger = new Mock<ILogger>();
			IServiceCollection services = new ServiceCollection();

			services.AddLogging(builder =>
			{
				builder.SetMinimumLevel(LogLevel.Trace);
				builder.AddConsole();
				builder.AddMock(fakeLogger);
			});

			IServiceProvider serviceProvider = services.BuildServiceProvider();

			ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
			logger = loggerFactory.CreateLogger("TestLogger");

			originalUser_encrypt = Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_USER_ENCRYPTED", EnvironmentVariableTarget.Machine) ?? "";
			originalUser = Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_USER") ?? "";

			originalPw_encrypt = Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED", EnvironmentVariableTarget.Machine) ?? "";
			originalPw = Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD") ?? "";


			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER", "");
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER_ENCRYPTED", "", EnvironmentVariableTarget.Machine);
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD", "");
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED", "", EnvironmentVariableTarget.Machine);
		}

		[TestCleanup()]
		public void Cleanup()
		{
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER", originalUser);
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER_ENCRYPTED", originalUser_encrypt, EnvironmentVariableTarget.Machine);
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD", originalPw);
			Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED", originalPw_encrypt, EnvironmentVariableTarget.Machine);
		}

		[TestMethod()]
		public async Task LocalTest_NoPath()
		{
			// Arrange
			Mock<IFileSystem> fs = new Mock<IFileSystem>();
			Mock<IFileIO> fakeFile = new Mock<IFileIO>();
			fs.Setup(p => p.File).Returns(fakeFile.Object);


			string pathToArtifact = "fake/path/artifactname.dmapp";
			string dmServerLocation = "fake.host.server";

			// Act
			Func<Task> deployAction = async () =>
			{
				var artifact = DeploymentFactory.Local(fs.Object, pathToArtifact, logger, dmServerLocation);
				var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));
			};

			// Assert
			await deployAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*path does not exist*");
		}

		[TestMethod()]
		public async Task LocalTest_NoPath()
		{
			// Arrange
			Mock<IFileSystem> fs = new Mock<IFileSystem>();
			Mock<IFileIO> fakeFile = new Mock<IFileIO>();
			fs.Setup(p => p.File).Returns(fakeFile.Object);


			string pathToArtifact = "fake/path/artifactname.dmapp";
			string dmServerLocation = "fake.host.server";

			// Act
			Func<Task> deployAction = async () =>
			{
				var artifact = DeploymentFactory.Local(fs.Object, pathToArtifact, logger, dmServerLocation);
				var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));
			};

			// Assert
			await deployAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*path does not exist*");
		}

		[TestMethod()]
		public async Task LocalTest_ArgumentKey_TimeOut()
		{
			// Arrange
			Mock<ICatalogService> fakeService = new Mock<ICatalogService>();
			string fakeCatalogId = "fakeId";
			string fakeToken = "agentToken";
			Guid guid = Guid.NewGuid();

			fakeService.Setup(p => p.DeployPackageAsync(fakeCatalogId, fakeToken, It.IsAny<CancellationToken>())).ReturnsAsync(new DeployingPackage("fakeId", guid));

			// Act
			Func<Task> deployAction = async () =>
			{
				var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", fakeToken, logger);
				var result = await artifact.DeployAsync(TimeSpan.FromSeconds(3));
			};

			// Assert
			await deployAction.Should().ThrowAsync<TimeoutException>().WithMessage("*Status was never succeeded, error or timeout*");
		}

		[TestMethod()]
		public async Task LocalTest_ArgumentKey_OK()
		{
			// Arrange
			Mock<ICatalogService> fakeService = new Mock<ICatalogService>();
			string fakeCatalogId = "fakeId";
			string fakeToken = "agentToken";
			Guid guid = Guid.NewGuid();
			var deployingPackage = new DeployingPackage("fakeId", guid);
			var deployedPackage = new DeployedPackage("succeeded");
			fakeService.Setup(p => p.DeployPackageAsync(fakeCatalogId, fakeToken, It.IsAny<CancellationToken>())).ReturnsAsync(deployingPackage);
			fakeService.Setup(p => p.GetDeployedPackageAsync(deployingPackage, fakeToken)).ReturnsAsync(deployedPackage);

			// Act
			var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", fakeToken, logger);
			var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));

			// Assert

			result.Should().BeTrue();

			fakeLogger.VerifyLog().InformationWasCalled().MessageEquals(@"{""Status"":""succeeded""}");
		}

		[TestMethod()]
		public async Task LocalTest_EnvKey_OK()
		{
			// Arrange
			Mock<ICatalogService> fakeService = new Mock<ICatalogService>();
			string fakeCatalogId = "fakeId";
			string fakeToken = "agentToken";

			Environment.SetEnvironmentVariable("DATAMINER_CATALOG_TOKEN", fakeToken);

			Guid guid = Guid.NewGuid();
			var deployingPackage = new DeployingPackage("fakeId", guid);
			var deployedPackage = new DeployedPackage("succeeded");
			fakeService.Setup(p => p.DeployPackageAsync(fakeCatalogId, fakeToken, It.IsAny<CancellationToken>())).ReturnsAsync(deployingPackage);
			fakeService.Setup(p => p.GetDeployedPackageAsync(deployingPackage, fakeToken)).ReturnsAsync(deployedPackage);

			// Act
			var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", logger);
			var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));

			// Assert
			result.Should().BeTrue();

			fakeLogger.VerifyLog().InformationWasCalled().MessageEquals(@"{""Status"":""succeeded""}");
		}

		[TestMethod()]
		public async Task LocalTest_EnvEncrypedKey_OK()
		{

		}
	}
}