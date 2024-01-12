namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService.SLNet
{
    using System;
    using System.Linq;
    using System.Threading;

    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.GRPCConnection;
    using Skyline.DataMiner.Net.Messages;

    internal sealed class SLNetCommunication : IDisposable
    {
        private SLNetCommunication(string hostname, string username, string password)
        {
            if (hostname.Contains(".dataminer.services"))
            {
                throw new InvalidOperationException("Unable to directly deploy to a cloud agent. Please use CatalogUpload tool and Deployment from-catalog with this tool.");
            }

            // Only works in .NET Framework
            //if (!hostname.ToLowerInvariant().Contains(".dataminer.services"))
            //{
            //	RemotingConnection.RegisterChannel();
            //}

            // Going to need to make a GRPC Connection directly. Remoting does NOT work on .NET6
            // Minimum Supported DataMiner is then: Main Release 10.3.0   february 2023   feature release 10.3.2
            
            Connection = new GRPCConnection(hostname);
            Connection.PollingRequestTimeout = 120000;
            Connection.ConnectTimeoutTime = 120000;
            Connection.AuthenticateMessageTimeout = 120000;

            Connection.Authenticate(username, password);

            EndPoint = hostname;
        }

        public Connection Connection { get; }

        public string EndPoint { get; private set; }

        public static SLNetCommunication GetConnection(string endUrlPoint, string username, string password)
        {
            return new SLNetCommunication(endUrlPoint, username, password);
        }

        public void Dispose()
        {
            Connection.Dispose();
        }


        public DMSMessage[] SendMessage(DMSMessage message)
        {

            var result = Connection.SendAsyncOverConnection(new[] { message }, 3600000);
            return result;
        }

        public DMSMessage SendSingleResponseMessage(DMSMessage message)
        {
            var result = Connection.SendAsyncOverConnection(new[] { message }, 3600000);
            return result.FirstOrDefault();
        }
    }
}