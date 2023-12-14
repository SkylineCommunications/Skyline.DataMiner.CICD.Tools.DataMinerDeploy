using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerArtifacts
{

	enum ArtifactTypeEnum
	{
		dmapp,
		legacyDmapp,
		dmprotocol,
	}

	internal class ArtifactType
	{
		public ArtifactTypeEnum Value { get; set; }

		public ArtifactType(string pathToArtifact)
		{
			if (pathToArtifact.EndsWith(".dmprotocol"))
			{
				Value = ArtifactTypeEnum.dmprotocol;
			}
			else
			{
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
							throw new InvalidOperationException($"Could not detect artifact type (.dmprotocol, dmapp or legacy dmapp) from the item {pathToArtifact}");
						}
					}
				}
			}
		}
	}
}
