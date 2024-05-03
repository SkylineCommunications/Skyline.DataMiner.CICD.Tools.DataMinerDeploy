namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib
{
    /// <summary>
    /// A series of actions to perform after deployment and installation has happened.
    /// </summary>
    public class PostDeployActions
    {
        /// <summary>
        /// Currently only works for local-artifact deployment directly to an agent and only for protocol packages (.dmprotocol).
        /// </summary>
        public (bool isTrue, bool copyTemplates) SetToProduction { get; set; }
    }
}