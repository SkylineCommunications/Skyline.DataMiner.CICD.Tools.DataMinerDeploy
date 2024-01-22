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
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService;

    [TestClass]
    public class DeploymentFactoryTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Mock<ILogger> fakeLogger;
        private ILogger logger;

        private string originalUser_encrypt;
        private string originalUser;
        private string originalPw_encrypt;
        private string originalPw;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [TestInitialize]
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

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER", originalUser);
            Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER_ENCRYPTED", originalUser_encrypt, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD", originalPw);
            Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED", originalPw_encrypt, EnvironmentVariableTarget.Machine);
        }

        [TestMethod]
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

        [TestMethod]
        public async Task LocalTest_NoKey()
        {
            // Arrange
            Mock<IFileSystem> fs = new Mock<IFileSystem>();
            Mock<IFileIO> fakeFile = new Mock<IFileIO>();
            Mock<IDataMinerService> fakeService = new Mock<IDataMinerService>();

            fs.Setup(p => p.File).Returns(fakeFile.Object);


            string pathToArtifact = "TestData/TestNewDmapp.dmapp";
            string dmServerLocation = "fake.host.server";

            fakeFile.Setup(p => p.Exists(pathToArtifact)).Returns(true);

            // Act
            Func<Task> deployAction = async () =>
            {
                var artifact = DeploymentFactory.Local(fs.Object, fakeService.Object, pathToArtifact, logger, dmServerLocation);
                var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));
            };

            // Assert
            await deployAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*is empty*");
        }

        [TestMethod]
        public async Task LocalTest_ArgumentKey_OK()
        {   // Arrange
            Mock<IFileSystem> fs = new Mock<IFileSystem>();
            Mock<IFileIO> fakeFile = new Mock<IFileIO>();
            Mock<IDataMinerService> fakeService = new Mock<IDataMinerService>();

            string pathToArtifact = "TestData/TestNewDmapp.dmapp";
            string dmServerLocation = "fake.host.server";
            string user = "fakeUser";
            string password = "fakePassword";

            fs.Setup(p => p.File).Returns(fakeFile.Object);
            fakeFile.Setup(p => p.Exists(pathToArtifact)).Returns(true);

            // Act
            var artifact = DeploymentFactory.Local(fs.Object, fakeService.Object, pathToArtifact, logger, dmServerLocation, user, password);
            var result = await artifact.DeployAsync(TimeSpan.FromSeconds(5));

            // Assert
            result.Should().BeTrue();

            fakeService.Verify(p => p.TryConnect(dmServerLocation, user, password));
            fakeService.Verify(p => p.InstallNewStyleAppPackages(pathToArtifact));
        }

        [TestMethod]
        public async Task LocalTest_EnvKey_OK()
        {
            // Arrange
            Mock<IFileSystem> fs = new Mock<IFileSystem>();
            Mock<IFileIO> fakeFile = new Mock<IFileIO>();
            Mock<IDataMinerService> fakeService = new Mock<IDataMinerService>();

            string pathToArtifact = "TestData/TestNewDmapp.dmapp";
            string dmServerLocation = "fake.host.server";
            string user = "fakeUserFromEnv";
            string password = "fakePasswordFomEnv";

            fs.Setup(p => p.File).Returns(fakeFile.Object);
            fakeFile.Setup(p => p.Exists(pathToArtifact)).Returns(true);

            Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_USER", user);
            Environment.SetEnvironmentVariable("DATAMINER_DEPLOY_PASSWORD", password);

            // Act
            var artifact = DeploymentFactory.Local(fs.Object, fakeService.Object, pathToArtifact, logger, dmServerLocation);
            var result = await artifact.DeployAsync(TimeSpan.FromSeconds(5));

            // Assert
            result.Should().BeTrue();
            fakeService.Verify(p => p.TryConnect(dmServerLocation, user, password));
            fakeService.Verify(p => p.InstallNewStyleAppPackages(pathToArtifact));

        }
        [TestMethod]
        public async Task LocalTest_EnvEncrypedKey_OK()
        {
            // Arrange
            Mock<IFileSystem> fs = new Mock<IFileSystem>();
            Mock<IFileIO> fakeFile = new Mock<IFileIO>();
            Mock<IDataMinerService> fakeService = new Mock<IDataMinerService>();

            string pathToArtifact = "TestData/TestNewDmapp.dmapp";
            string dmServerLocation = "fake.host.server";
            string user = "fakeUserFromEncryptedEnv";
            string password = "fakePasswordFomEncryptedEnv";

            fs.Setup(p => p.File).Returns(fakeFile.Object);
            fakeFile.Setup(p => p.Exists(pathToArtifact)).Returns(true);

            WinEncryptedKeys.Lib.Keys.SetKey("DATAMINER_DEPLOY_USER_ENCRYPTED", user);
            WinEncryptedKeys.Lib.Keys.SetKey("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED", password);

            // Act
            var artifact = DeploymentFactory.Local(fs.Object, fakeService.Object, pathToArtifact, logger, dmServerLocation);
            var result = await artifact.DeployAsync(TimeSpan.FromSeconds(5));

            // Assert
            result.Should().BeTrue();
            fakeService.Verify(p => p.TryConnect(dmServerLocation, user, password));
            fakeService.Verify(p => p.InstallNewStyleAppPackages(pathToArtifact));
        }

        [TestMethod]
        public async Task LocalTest_ArgumentKey_OldDmapp()
        {   // Arrange
            Mock<IFileSystem> fs = new Mock<IFileSystem>();
            Mock<IFileIO> fakeFile = new Mock<IFileIO>();
            Mock<IDataMinerService> fakeService = new Mock<IDataMinerService>();

            string pathToArtifact = "TestData/TestOldDmapp.dmapp";
            string dmServerLocation = "fake.host.server";
            string user = "fakeUser";
            string password = "fakePassword";

            fs.Setup(p => p.File).Returns(fakeFile.Object);
            fakeFile.Setup(p => p.Exists(pathToArtifact)).Returns(true);

            // Act
            var artifact = DeploymentFactory.Local(fs.Object, fakeService.Object, pathToArtifact, logger, dmServerLocation, user, password);
            var result = await artifact.DeployAsync(TimeSpan.FromSeconds(5));

            // Assert
            result.Should().BeTrue();

            fakeService.Verify(p => p.TryConnect(dmServerLocation, user, password));
            fakeService.Verify(p => p.InstallLegacyStyleAppPackages(pathToArtifact, TimeSpan.FromSeconds(5)));
        }

        [TestMethod]
        public async Task LocalTest_ArgumentKey_Protocol()
        {   // Arrange
            Mock<IFileSystem> fs = new Mock<IFileSystem>();
            Mock<IFileIO> fakeFile = new Mock<IFileIO>();
            Mock<IDataMinerService> fakeService = new Mock<IDataMinerService>();

            string pathToArtifact = "TestData/TestProtocol.dmprotocol";
            string dmServerLocation = "fake.host.server";
            string user = "fakeUser";
            string password = "fakePassword";

            fs.Setup(p => p.File).Returns(fakeFile.Object);
            fakeFile.Setup(p => p.Exists(pathToArtifact)).Returns(true);

            // Act
            var artifact = DeploymentFactory.Local(fs.Object, fakeService.Object, pathToArtifact, logger, dmServerLocation, user, password);
            var result = await artifact.DeployAsync(TimeSpan.FromSeconds(5));

            // Assert
            result.Should().BeTrue();

            fakeService.Verify(p => p.TryConnect(dmServerLocation, user, password));
            var setToProduction = (false, false);
            fakeService.Verify(p => p.InstallDataMinerProtocol(pathToArtifact, setToProduction));
        }
    }
}