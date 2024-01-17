namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.Tests
{
    using System;

    using FluentAssertions;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Mock;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Moq;

    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib;

    [TestClass]
    public class CloudDeploymentFactoryTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Mock<ILogger> fakeLogger;
        private ILogger logger;

        private string originalKey_encrypt;
        private string originalKey;
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

            originalKey_encrypt = Environment.GetEnvironmentVariable("DATAMINER_CATALOG_TOKEN_ENCRYPTED", EnvironmentVariableTarget.Machine) ?? "";
            originalKey = Environment.GetEnvironmentVariable("DATAMINER_CATALOG_TOKEN") ?? "";

            Environment.SetEnvironmentVariable("DATAMINER_CATALOG_TOKEN", "");
            Environment.SetEnvironmentVariable("DATAMINER_CATALOG_TOKEN_ENCRYPTED", "", EnvironmentVariableTarget.Machine);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable("DATAMINER_CATALOG_TOKEN_ENCRYPTED", originalKey_encrypt, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("DATAMINER_CATALOG_TOKEN", originalKey);
        }

        [TestMethod]
        public async Task CloudTest_NoKey()
        {
            // Arrange
            Mock<ICatalogService> fakeService = new Mock<ICatalogService>();

            // Act
            Func<Task> deployAction = async () =>
            {
                using (var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", logger))
                {
                    var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));
                }
            };

            // Assert
            await deployAction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*missing token*");
        }

        [TestMethod]
        public async Task CloudTest_ArgumentKey_TimeOut()
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
                using (var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", fakeToken, logger))
                {
                    var result = await artifact.DeployAsync(TimeSpan.FromSeconds(3));
                }
            };

            // Assert
            await deployAction.Should().ThrowAsync<TimeoutException>().WithMessage("*Status was never 'succeeded', 'error' or 'timeout'*");
        }

        [TestMethod]
        public async Task CloudTest_ArgumentKey_OK()
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
            using (var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", fakeToken, logger))
            {
                var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));

                // Assert

                result.Should().BeTrue();

                fakeLogger.VerifyLog().InformationWasCalled().MessageEquals(@"{""Status"":""succeeded""}");
            }
        }

        [TestMethod]
        public async Task CloudTest_EnvKey_OK()
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
            using (var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", logger))
            {
                var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));

                // Assert
                result.Should().BeTrue();

                fakeLogger.VerifyLog().InformationWasCalled().MessageEquals(@"{""Status"":""succeeded""}");
            }
        }

        [TestMethod]
        public async Task CloudTest_EnvEncrypedKey_OK()
        {
            // Arrange
            Mock<ICatalogService> fakeService = new Mock<ICatalogService>();
            string fakeCatalogId = "fakeId";
            string fakeToken = "agentToken";

            WinEncryptedKeys.Lib.Keys.SetKey("DATAMINER_CATALOG_TOKEN_ENCRYPTED", fakeToken);

            Guid guid = Guid.NewGuid();
            var deployingPackage = new DeployingPackage("fakeId", guid);
            var deployedPackage = new DeployedPackage("succeeded");
            fakeService.Setup(p => p.DeployPackageAsync(fakeCatalogId, fakeToken, It.IsAny<CancellationToken>())).ReturnsAsync(deployingPackage);
            fakeService.Setup(p => p.GetDeployedPackageAsync(deployingPackage, fakeToken)).ReturnsAsync(deployedPackage);

            // Act
            using (var artifact = DeploymentFactory.Cloud(fakeService.Object, "fakeId", logger))
            {
                var result = await artifact.DeployAsync(TimeSpan.FromSeconds(10));

                // Assert
                result.Should().BeTrue();

                fakeLogger.VerifyLog().InformationWasCalled().MessageEquals(@"{""Status"":""succeeded""}");
            }
        }
    }
}