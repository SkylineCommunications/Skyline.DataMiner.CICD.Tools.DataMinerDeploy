namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerArtifacts
{
    using System;
    using System.IO.Compression;

    using Skyline.DataMiner.CICD.FileSystem;

    internal enum ArtifactTypeEnum
    {
        unknown,
        dmapp,
        legacyDmapp,
        dmprotocol,
    }

    internal class ArtifactType
    {
        public ArtifactType(IFileSystem fs, string pathToArtifact)
        {
            if (!fs.File.Exists(pathToArtifact))
            {
                throw new ArgumentException($"Could not find artifact in provided path {pathToArtifact}", nameof(pathToArtifact));
            }

            if (pathToArtifact.EndsWith(".dmprotocol"))
            {
                Value = ArtifactTypeEnum.dmprotocol;
                return;
            }

            using (var zipFile = ZipFile.OpenRead(pathToArtifact))
            {
                ZipArchiveEntry foundAppInfo = zipFile.GetEntry("AppInfo.xml");
                if (foundAppInfo != null)
                {
                    Value = ArtifactTypeEnum.dmapp;
                }
                else
                {
                    ZipArchiveEntry foundUpgradeDll = zipFile.GetEntry("SLUpgrade.dll");
                    if (foundUpgradeDll != null)
                    {
                        Value = ArtifactTypeEnum.legacyDmapp;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Could not detect artifact type (.dmprotocol, dmapp or legacy dmapp) from the item {pathToArtifact}");
                    }
                }
            }
        }

        public ArtifactTypeEnum Value { get; set; }
    }
}