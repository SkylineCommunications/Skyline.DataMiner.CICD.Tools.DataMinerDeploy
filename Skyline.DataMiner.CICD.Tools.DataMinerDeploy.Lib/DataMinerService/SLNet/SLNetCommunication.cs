﻿namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.DataMinerService.SLNet
{
    using System;
    using System.Linq;

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

            try
            {
                Connection = new GRPCConnection(hostname);
            }
            catch (Exception ex)
            {
                // DataMinerOfflineException when APIGateway is not present or too low version. (Tested on DM 10.1 & 10.2)
                // Keeping it a generic catch to deal with possible differences between DM versions. Error should be clearer instead of an unclear error from DataMiner.

                throw new InvalidOperationException("Unable to reach DataMiner. Make sure that DataMiner and APIGateway are up and running and DataMiner has a minimum version of MR 10.3 / FR 10.3.2 ", ex);
            }

            Connection.PollingRequestTimeout = 120000;
            Connection.ConnectTimeoutTime = 120000;
            Connection.AuthenticateMessageTimeout = 120000;
            Connection.Authenticate(username, password);

            // Required to allow async slnet calls that we need for installing.
            Connection.Subscribe(new SubscriptionFilter());

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