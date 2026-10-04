using System;
using Azure;
using System.Globalization;
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
        {
            ApplicationName = applicationName ?? string.Empty;
            PartitionKey = AzureHelper.EncodeAzureKey(applicationName);
            // Reverse ticks keep newest-first ordering; the suffix avoids collisions.
            RowKey = (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString("d19", CultureInfo.InvariantCulture)
                + "_" + Guid.NewGuid().ToString("N");
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
