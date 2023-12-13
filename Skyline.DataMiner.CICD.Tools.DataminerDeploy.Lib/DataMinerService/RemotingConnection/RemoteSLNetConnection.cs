namespace Skyline.DataMiner.CICD.Tools.DataMinerDeploy.Lib.RemotingConnection
{
	using System;
	using System.Linq;
	using System.Threading;

	internal enum AlarmLevel
	{
		Critical = 1,
		Major,
		Minor,
		Warning,
		Normal
	}
	internal sealed class RemoteSLNetConnection : IDisposable
	{
		private readonly Skyline.DataMiner.Net.RemotingConnection _connection;
		private RemoteSLNetConnection(string endUrlPoint, string username, string password)
		{
			Skyline.DataMiner.Net.RemotingConnection.RegisterChannel();
			_connection = new Skyline.DataMiner.Net.RemotingConnection(endUrlPoint);
			_connection.Authenticate(username, password);

			this.EndPoint = endUrlPoint;
			Connection.Subscribe(new Skyline.DataMiner.Net.SubscriptionFilter());
		}

		public Skyline.DataMiner.Net.RemotingConnection Connection
		{
			get { return _connection; }
		}

		public string EndPoint { get; private set; }

		public static RemoteSLNetConnection GetConnection(string endUrlPoint, string username, string password)
		{
			return new RemoteSLNetConnection(endUrlPoint, username, password);
		}

		public static RemoteSLNetConnection GetConnection(string endUrlPoint, string username, string password, int retryInterval, int retries)
		{
			int i = 0;
			while (true)
			{
				try
				{
					RemoteSLNetConnection connection = new RemoteSLNetConnection(endUrlPoint, username, password);
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