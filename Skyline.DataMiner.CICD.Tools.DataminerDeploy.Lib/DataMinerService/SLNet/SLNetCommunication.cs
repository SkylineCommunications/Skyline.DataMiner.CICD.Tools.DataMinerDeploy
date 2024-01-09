namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.RemotingConnection
{
	using System;
	using System.Linq;
	using System.Runtime;
	using System.Threading;

	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.GRPCConnection;

	internal enum AlarmLevel
	{
		Critical = 1,
		Major,
		Minor,
		Warning,
		Normal
	}
	internal sealed class SLNetCommunication : IDisposable
	{
		private readonly Skyline.DataMiner.Net.Connection _connection;

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


			_connection = new GRPCConnection(hostname);
			_connection.Authenticate(username, password);
			_connection.Subscribe(new Skyline.DataMiner.Net.SubscriptionFilter());

			this.EndPoint = hostname;
		}

		public Skyline.DataMiner.Net.Connection Connection
		{
			get { return _connection; }
		}

		public string EndPoint { get; private set; }

		public static SLNetCommunication GetConnection(string endUrlPoint, string username, string password)
		{
			return new SLNetCommunication(endUrlPoint, username, password);
		}

		public static SLNetCommunication GetConnection(string endUrlPoint, string username, string password, int retryInterval, int retries)
		{
			int i = 0;
			while (true)
			{
				try
				{
					SLNetCommunication connection = new SLNetCommunication(endUrlPoint, username, password);
					return connection;
				}
				catch (Exception e)
				{
					if (i == retries)
					{
						string message = String.Format("Error while creating connection after {0} retries. Last exception :\n{1}", retries, e);
						throw new TimeoutException(message);
					}
					else
					{
						i++;
						System.Threading.Thread.Sleep(retryInterval);
					}
				}
			}
		}

		public void Dispose()
		{
			_connection.Dispose();
		}


		public Skyline.DataMiner.Net.Messages.DMSMessage[] SendMessage(Skyline.DataMiner.Net.Messages.DMSMessage message)
		{

			var result = Connection.SendAsyncOverConnection(new[] { message }, 3600000);
			return result;
		}

		public Skyline.DataMiner.Net.Messages.DMSMessage SendSingleResponseMessage(Skyline.DataMiner.Net.Messages.DMSMessage message)
		{
			var result = Connection.SendAsyncOverConnection(new[] { message }, 3600000);
			return result.FirstOrDefault();
		}
	}
}