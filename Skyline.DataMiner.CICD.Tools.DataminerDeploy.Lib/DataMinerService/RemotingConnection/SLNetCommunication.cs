namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.RemotingConnection
{
	using System;
	using System.Linq;
	using System.Runtime;
	using System.Threading;

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

		private SLNetCommunication(string endUrlPoint, string username, string password)
		{
			// Doing this to support some older DataMiner versions.
			Net.RemotingConnection.RegisterChannel();
			_connection = Skyline.DataMiner.Net.ConnectionSettings.GetConnection(endUrlPoint);
			// _connection.ClientApplicationName = "VSTEST.CONSOLE.EXE"; <-- TODO: check if we need this still?

			_connection.Authenticate(username, password);
			_connection.Subscribe(new Skyline.DataMiner.Net.SubscriptionFilter());

			this.EndPoint = endUrlPoint;
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

			var result = Connection.SendAsyncOverConnection(new[] {message}, 3600000);
			return result;
		}

		public Skyline.DataMiner.Net.Messages.DMSMessage SendSingleResponseMessage(Skyline.DataMiner.Net.Messages.DMSMessage message)
		{
			var result = Connection.SendAsyncOverConnection(new[] { message }, 3600000);
			return result.FirstOrDefault();
		}
	}
}