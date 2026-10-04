using Azure.Data.Tables;
using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace Elmah.AzureTableStorage
{
    /// <summary>
    /// An <see cref="ErrorLog"/> implementation that uses Microsoft Azure 
    /// Storage as its backing store.
    /// </summary>

    public class AzureTableStorageErrorLog : ErrorLog
    {
        private readonly TableClient _tableClient;
        private const string DefaultTableName = "Elmah";
        private const string TableValidationRegex = @"\A[A-Za-z][A-Za-z0-9]{2,62}\z";

        private const int MaxAppNameLength = 60;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureTableStorageErrorLog"/> class
        /// using a dictionary of configured settings.
        /// </summary>

        public AzureTableStorageErrorLog(IDictionary config)
            : this(config, (connectionString, tableName) => new TableClient(connectionString, tableName))
        {
        }

        internal AzureTableStorageErrorLog(IDictionary config, Func<string, string, TableClient> createClient)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            if (createClient == null)
                throw new ArgumentNullException(nameof(createClient));

            var connectionString = ElmahHelper.GetConnectionString(config);

            //
            // If there is no connection string to use then throw an 
            // exception to abort construction.
            //

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ApplicationException("Connection string is missing for the Azure Table Storage error log.");

            //
            // Get custom table name for storage and validate
            //

            var tableName = config.Find("tableName", DefaultTableName);

            if(!Regex.IsMatch(tableName, TableValidationRegex))
                throw new ApplicationException("Name for table in Azure Table Storage is not a valid name.");

            //
            // Set the application name as this implementation provides
            // per-application isolation over a single store.
            //

            var appName = config.Find("applicationName", string.Empty);

            if (appName.Length > MaxAppNameLength)
            {
                throw new ApplicationException(string.Format(
                    "Application name is too long. Maximum length allowed is {0} characters.",
                    MaxAppNameLength.ToString("N0")));
            }

            ApplicationName = appName;
            _tableClient = createClient(connectionString, tableName);
            _tableClient.CreateIfNotExists();
        }

        /// <summary>
        /// Gets the name of this error log implementation.
        /// </summary>

        public override string Name
        {
            get { return "Microsoft Azure Storage Error Log"; }
        }

        /// <summary>
        /// Logs an error to the database.
        /// </summary>
        /// <remarks>
        /// Writes synchronously so callers observe storage failures before an identifier is returned.
        /// </remarks>

        public override string Log(Error error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            var elmahEntity = new ElmahEntity(ApplicationName)
            {
                AllXml = ErrorXml.EncodeString(error),
                ApplicationName = ApplicationName,
                HostName = error.HostName,
                Message = error.Message,
                Source = error.Source,
                StatusCode = error.StatusCode,
                Type = error.Type,
                User = error.User,
            };

            _tableClient.AddEntity(elmahEntity);

            return elmahEntity.RowKey;
        }

        /// <summary>
        /// Returns a page of errors from the database in descending order 
        /// of logged time.
        /// </summary>

        public override int GetErrors(int pageIndex, int pageSize, IList errorEntryList)
        {
            if (pageIndex < 0) throw new ArgumentOutOfRangeException("pageIndex", pageIndex, null);
            if (pageSize < 0) throw new ArgumentOutOfRangeException("pageSize", pageSize, null);

            if (pageSize == 0) return 0;
            var offset = (long)pageIndex * pageSize;
            if (offset + pageSize >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(pageIndex), "Requested page exceeds the supported record count.");

            // Azure tables have no offset operator. Stop after one lookahead record
            // instead of loading the entire application's error log.
            var partitionKey = AzureHelper.EncodeAzureKey(ApplicationName);
            var errorEntities = _tableClient.Query<ElmahEntity>(e => e.PartitionKey == partitionKey, maxPerPage: pageSize);
            long visited = 0;
            var added = 0;

            foreach (var errorEntity in errorEntities)
            {
                if (visited++ < offset) continue;
                if (added == pageSize) return (int)offset + added + 1;
                if (errorEntryList != null)
                    errorEntryList.Add(new ErrorLogEntry(this, errorEntity.RowKey, ErrorXml.DecodeString(errorEntity.AllXml)));
                added++;
            }

            // This is an exact count if enumeration reached the end; otherwise
            // the lookahead above provides a lower bound for ELMAH's next-page link.
            return (int)visited;
        }

        /// <summary>
        /// Returns the specified error from the database, or null 
        /// if it does not exist.
        /// </summary>

        public override ErrorLogEntry GetError(string id)
        {
            if (id == null) throw new ArgumentNullException("id");
            if (id.Length == 0) throw new ArgumentException(null, "id");

            var response = _tableClient.GetEntityIfExists<ElmahEntity>(AzureHelper.EncodeAzureKey(ApplicationName), id);
            return response.HasValue
                ? new ErrorLogEntry(this, id, ErrorXml.DecodeString(response.Value.AllXml))
                : null;
        }
    }
}
