using System;
using Azure;
using Azure.Data.Tables;

namespace Elmah.AzureTableStorage
{
    public class ElmahEntity : ITableEntity
    {
        public ElmahEntity()
        {
            // Azure Table Storage requires an empty constructor.
        }

        public ElmahEntity(string applicationName)
            : base()
        {
            ApplicationName = applicationName;
            PartitionKey = AzureHelper.EncodeAzureKey(applicationName);
            RowKey = (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19");
        }

        public string ApplicationName { get; set; }
        public string HostName { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public string User { get; set; }
        public int StatusCode { get; set; }
        public string AllXml { get; set; }
        public string PartitionKey { get; set; }

        // additional implementation of ITableEntity
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
