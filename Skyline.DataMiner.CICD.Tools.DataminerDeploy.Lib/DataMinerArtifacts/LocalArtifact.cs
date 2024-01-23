namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerArtifacts;
    using Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.SLDataGateway.Helpers;

    internal class LocalArtifact : IArtifact
    {
        private readonly string dataminerPassword;
        private readonly string dataMinerServerLocation;
        private readonly string dataminerUser;
        private readonly IFileSystem fs;
        private readonly ILogger logger;
        private readonly string pathToArtifact;
        private readonly IDataMinerService service;
        private bool disposedValue;
        private PostDeployActions postDeployActions;
        private string pwFromEnv;
        private bool shouldDisposeConnection = true;
        private string userFromEnv;

        public LocalArtifact(IDataMinerService dataMinerService, string pathToArtifact, string dataMinerServerLocation, string dataMinerUser, string dataMinerPassword, ILogger logger, IFileSystem fs)
        {
            if (String.IsNullOrWhiteSpace(pathToArtifact))
            {
                throw new ArgumentNullException(nameof(pathToArtifact));
            }

            this.fs = fs;
            service = dataMinerService ?? throw new ArgumentNullException(nameof(dataMinerService));
            this.pathToArtifact = pathToArtifact;
            this.dataMinerServerLocation = dataMinerServerLocation;
            dataminerUser = dataMinerUser;
            dataminerPassword = dataMinerPassword;
            this.logger = logger;
            TryFindEnvironmentKeys();
        }

        public LocalArtifact(IDataMinerService dataMinerService, string pathToArtifact, string dataMinerServerLocation, ILogger logger, IFileSystem fs) : this(dataMinerService, pathToArtifact, dataMinerServerLocation, null, null, logger, fs)
        {
            shouldDisposeConnection = false;
        }

        public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, string dataMinerUser, string dataMinerPassword, ILogger logger, IFileSystem fs) : this(DataMinerServiceFactory.CreateWithSLNet(fs, logger), pathToArtifact, dataMinerServerLocation, dataMinerUser, dataMinerPassword, logger, fs)
        {
        }

        public LocalArtifact(string pathToArtifact, string dataMinerServerLocation, ILogger logger, IFileSystem fs) : this(pathToArtifact, dataMinerServerLocation, null, null, logger, fs)
        {
        }

        /// <summary>
        /// Adds a series of actions to attempt after deployment.
        /// </summary>
        /// <param name="postDeployActions">An instance of <see cref="PostDeployActions"/> indicating what actions to try performing after deployment.</param>
        public void AddPostDeployActions(PostDeployActions postDeployActions)
        {
            this.postDeployActions = postDeployActions;
        }

        public void CancelDeployment()
        {
            throw new InvalidOperationException("Unable to cancel deployment of a local artifact.");
        }

        public async Task<bool> DeployAsync(TimeSpan timeout)
        {
            return await Task.Factory.StartNew(() =>
            {
                if (!fs.File.Exists(pathToArtifact))
                {
                    throw new InvalidOperationException($"Unable to deploy, path does not exist: {pathToArtifact}");
                }

                string actualUser;
                string actualPassword;
                if (!String.IsNullOrWhiteSpace(dataminerUser))
                {
                    actualUser = dataminerUser;
                    logger.LogDebug("User provided through arguments, this takes precedence over environment variables.");
                }
                else
                {
                    actualUser = userFromEnv;
                }

                if (!String.IsNullOrWhiteSpace(dataminerPassword))
                {
                    actualPassword = dataminerPassword;
                    logger.LogDebug("Password provided through arguments, this takes precedence over environment variables.");
                }
                else
                {
                    actualPassword = pwFromEnv;
                }

                if (String.IsNullOrEmpty(actualUser) || String.IsNullOrEmpty(actualPassword))
                {
                    throw new InvalidOperationException("Username or password is empty. Expected credentials either provided through arguments or with Environment Variables DATAMINER_DEPLOY_USER_ENCRYPTED/DATAMINER_DEPLOY_PASSWORD_ENCRYPTED or DATAMINER_DEPLOY_USER/DATAMINER_DEPLOY_PASSWORD.");
                }

                logger.LogDebug($"Setting up connection to {dataMinerServerLocation}...");
                service.TryConnect(dataMinerServerLocation, actualUser, actualPassword);
                logger.LogDebug($"Connection established.");
                ArtifactType type = new ArtifactType(fs, pathToArtifact);

                switch (type.Value)
                {
                    case ArtifactTypeEnum.dmapp:
                        logger.LogDebug($"Found DataMiner application installation package (.dmapp).");
                        service.InstallNewStyleAppPackages(pathToArtifact);
                        break;

                    case ArtifactTypeEnum.legacyDmapp:
                        logger.LogDebug($"Found legacy DataMiner application installation package (.dmapp).");
                        service.InstallLegacyStyleAppPackages(pathToArtifact, timeout);
                        break;

                    case ArtifactTypeEnum.dmprotocol:
                        logger.LogDebug($"Found DataMiner protocol package (.dmprotocol).");
                        service.InstallDataMinerProtocol(pathToArtifact, postDeployActions?.SetToProduction ?? (false, false));
                        break;

                    default:
                        break;
                }

                DeployedPackage output = new DeployedPackage("succeeded");
                logger.LogInformation(JsonConvert.SerializeObject(output));
                return true;
            }).TimeoutAfter(timeout);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (shouldDisposeConnection)
                    {
                        service.Dispose();
                    }
                    // Nothing to dispose right now
                    //cancellationTokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        private string TryFindEncryptedEnvironmentKey(string key)
        {
            try
            {
                var encryptedKey = WinEncryptedKeys.Lib.Keys.RetrieveKey(key);
                if (encryptedKey != null)
                {
                    string keyFromWinEncryptedKeys = new System.Net.NetworkCredential(String.Empty, encryptedKey).Password;

                    if (!String.IsNullOrWhiteSpace(keyFromWinEncryptedKeys))
                    {
                        logger.LogDebug($"OK: Found token in Env Variable: '{key}' created by WinEncryptedKeys.");
                        return keyFromWinEncryptedKeys;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Gobble up, no key means we try the next thing.
            }

            return null;
        }

        private string TryFindEnvironmentKey(string key)
        {
            string userFromEnvironment = Environment.GetEnvironmentVariable(key);

            if (String.IsNullOrWhiteSpace(userFromEnvironment))
            {
                return null;
            }

            if (!String.IsNullOrWhiteSpace(userFromEnv))
            {
                logger.LogDebug("OK: Overriding previously encrypted key with found token in Env Variable: 'DATAMINER_DEPLOY_USER'.");
            }
            else
            {
                logger.LogDebug("OK: Found token in Env Variable: 'DATAMINER_DEPLOY_USER'.");
            }

            return userFromEnvironment;
        }

        /// <summary>
        ///  Attempts to find the necessary API key in Environment Variables. In order of priority:
        ///  <para>- key stored as an Environment Variable called "dmcatalogtoken". (unix/win)</para>
        ///  <para>- key configured using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys called "dmcatalogtoken_encrypted" (windows only)</para>
        /// </summary>
        private void TryFindEnvironmentKeys()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Order of priority. Priority for regular environment keys as they are win/unix and industry standard in pipelines
                userFromEnv = TryFindEncryptedEnvironmentKey("DATAMINER_DEPLOY_USER_ENCRYPTED") ?? userFromEnv;
                pwFromEnv = TryFindEncryptedEnvironmentKey("DATAMINER_DEPLOY_PASSWORD_ENCRYPTED") ?? pwFromEnv;

                userFromEnv = TryFindEnvironmentKey("DATAMINER_DEPLOY_USER") ?? userFromEnv;
                pwFromEnv = TryFindEnvironmentKey("DATAMINER_DEPLOY_PASSWORD") ?? pwFromEnv;
            }
        }
    }
}