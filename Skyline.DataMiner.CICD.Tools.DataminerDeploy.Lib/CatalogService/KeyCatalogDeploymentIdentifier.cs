namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.CatalogService
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class KeyCatalogDeploymentIdentifier
    {
        public string CatalogGuid { get; set; }
        public string CatalogVersion { get; set; }
        public string DestinationGuid { get; set; }
    }
}
