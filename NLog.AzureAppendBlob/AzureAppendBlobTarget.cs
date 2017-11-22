using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NLog.AzureAppendBlob
{
    [Target("AzureAppendBlob")]
	public sealed class AzureAppendBlobTarget : TargetWithLayout
	{
		[RequiredParameter]
		public Layout ConnectionString { get; set; }

		[RequiredParameter]
		public Layout Container { get; set; }

		[RequiredParameter]
		public Layout BlobName { get; set; }

		private CloudBlobClient _client;
		private CloudBlobContainer _container;
		private CloudAppendBlob _blob;
		private string _connectionString;

		private const string NewLine = "\r\n";

		protected override void InitializeTarget()
		{
			base.InitializeTarget();

			_client = null;
		}
        
        protected override void Write(IList<AsyncLogEventInfo> logEvents)
		{
			if (logEvents.Count == 0)
			{
				return;
			}

			ConnectToBlob(logEvents[0].LogEvent).Wait();

			try
			{
				using (var stream = new MemoryStream())
				using (var writer = new StreamWriter(stream))
				{
					for (int i = 0; i < logEvents.Count; ++i)
					{
						this.MergeEventProperties(logEvents[i].LogEvent);
						writer.WriteLine(Layout.Render(logEvents[i].LogEvent));
					}

					writer.Flush();

					stream.Seek(0, SeekOrigin.Begin);
					_blob.AppendBlockAsync(stream, null).Wait();
				}

				for (int i = 0; i < logEvents.Count; ++i)
				{
					logEvents[i].Continuation(null);
				}
			}
			catch (Exception exception)
			{
				for (int i = 0; i < logEvents.Count; ++i)
				{
					logEvents[i].Continuation(exception);
				}
			}
		}

		protected override void Write(AsyncLogEventInfo logEvent)
		{
			// just flow through and use base functionality, forward call to Write(LogEventInfo)
			base.Write(logEvent);
		}

		protected override void Write(LogEventInfo logEvent)
		{
			ConnectToBlob(logEvent).Wait();
			_blob.AppendTextAsync(Layout.Render(logEvent) + NewLine).Wait();
		}
		
		private async Task ConnectToBlob(LogEventInfo logEvent)
		{
			var connectionString = ConnectionString.Render(logEvent);

			if (_client == null || !string.Equals(_connectionString, connectionString, System.StringComparison.OrdinalIgnoreCase))
			{
				_client = CloudStorageAccount.Parse(connectionString).CreateCloudBlobClient();
				InternalLogger.Debug("Initialized connection to {0}", connectionString);
				_connectionString = connectionString;

			}

			string containerName = Container.Render(logEvent);
			string blobName = BlobName.Render(logEvent);

			if (_container == null || _container.Name != containerName)
			{
				_container = _client.GetContainerReference(containerName);
				InternalLogger.Debug("Got container reference to {0}", containerName);
                
                if (await _container.CreateIfNotExistsAsync())
				{
					InternalLogger.Debug("Created container {0}", containerName);
				}

				_blob = null;
			}

			if (_blob == null || _blob.Name != blobName)
			{
				_blob = _container.GetAppendBlobReference(blobName);

				if (!await _blob.ExistsAsync())
				{
					try
					{
						_blob.Properties.ContentType = "text/plain";
						await _blob.CreateOrReplaceAsync();
						InternalLogger.Debug("Created blob: {0}", blobName);
					}
					catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
					{
						// to be expected
					}
				}
			}
		}
	}
}
