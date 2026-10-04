using System;
using System.Collections;
using System.Linq;
using Azure;
using NUnit.Framework;

namespace Elmah.AzureTableStorage.Tests
{
    [TestFixture]
    public class ErrorLogTests
    {
        private FakeTableClient _client;
        private AzureTableStorageErrorLog _log;

        private static Hashtable Config(string app = "test-app") => new Hashtable
        {
            ["connectionString"] = "UseDevelopmentStorage=true",
            ["applicationName"] = app
        };

        [SetUp]
        public void SetUp()
        {
            _client = new FakeTableClient();
            _log = new AzureTableStorageErrorLog(Config(), (connection, table) => _client);
        }

        private void Seed(int count, string app = "test-app")
        {
            for (var i = 0; i < count; i++)
            {
                var error = new Error(new InvalidOperationException("error-" + i));
                _client.Entities.Add(new ElmahEntity(app)
                {
                    RowKey = i.ToString("d19"),
                    AllXml = ErrorXml.EncodeString(error)
                });
            }
        }

        [Test]
        public void ConstructorUsesDefaultTableAndCreatesItOnce()
        {
            string connection = null, table = null;
            var client = new FakeTableClient();
            var log = new AzureTableStorageErrorLog(Config(), (c, t) => { connection = c; table = t; return client; });
            Assert.That(connection, Is.EqualTo("UseDevelopmentStorage=true"));
            Assert.That(table, Is.EqualTo("Elmah"));
            Assert.That(client.CreateCalls, Is.EqualTo(1));
            Assert.That(log.ApplicationName, Is.EqualTo("test-app"));
            Assert.That(log.Name, Is.EqualTo("Microsoft Azure Storage Error Log"));
        }

        [Test]
        public void ConstructorUsesCustomTableAndAcceptsMaximumApplicationLength()
        {
            var config = Config(new string('a', 60));
            config["tableName"] = new string('A', 63);
            string table = null;
            var log = new AzureTableStorageErrorLog(config, (c, t) => { table = t; return _client; });
            Assert.That(table, Is.EqualTo(config["tableName"]));
            Assert.That(log.ApplicationName.Length, Is.EqualTo(60));
        }

        [TestCase("")]
        [TestCase("ab")]
        [TestCase("1abc")]
        [TestCase("abc_def")]
        [TestCase("abc-def")]
        [TestCase("abc\n")]
        public void InvalidTableIsRejectedBeforeCreatingClient(string table)
        {
            var config = Config();
            config["tableName"] = table;
            var invoked = false;
            Assert.Throws<ApplicationException>(() => new AzureTableStorageErrorLog(config,
                (c, t) => { invoked = true; return _client; }));
            Assert.That(invoked, Is.False);
        }

        [Test]
        public void ExcessiveTableLengthIsRejected()
        {
            var config = Config();
            config["tableName"] = new string('a', 64);
            Assert.Throws<ApplicationException>(() => new AzureTableStorageErrorLog(config, (c, t) => _client));
        }

        [Test]
        public void ExcessiveApplicationLengthIsRejectedBeforeClientCreation()
        {
            var invoked = false;
            Assert.Throws<ApplicationException>(() => new AzureTableStorageErrorLog(Config(new string('a', 61)),
                (c, t) => { invoked = true; return _client; }));
            Assert.That(invoked, Is.False);
        }

        [Test]
        public void NullConfigurationAndFactoryAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new AzureTableStorageErrorLog(null));
            Assert.Throws<ArgumentNullException>(() => new AzureTableStorageErrorLog(Config(), null));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MissingConnectionStringIsRejected(string value)
        {
            var config = Config();
            config["connectionString"] = value;
            Assert.Throws<ApplicationException>(() => new AzureTableStorageErrorLog(config));
        }

        [Test]
        public void InvalidStorageConnectionStringIsRejectedWithoutNetworkAccess()
        {
            var config = Config();
            config["connectionString"] = "invalid";
            Assert.Throws<InvalidOperationException>(() => new AzureTableStorageErrorLog(config));
        }

        [Test]
        public void StorageCreationFailuresArePropagated()
        {
            _client.CreateFailure = new RequestFailedException(403, "Denied");
            Assert.Throws<RequestFailedException>(() => new AzureTableStorageErrorLog(Config(), (c, t) => _client));
        }

        [Test]
        public void LogPersistsAllMetadataAndXmlBeforeReturning()
        {
            var error = new Error(new InvalidOperationException("failure"))
            {
                HostName = "localhost", User = "tester", StatusCode = 503
            };
            var id = _log.Log(error);
            var entity = _client.Entities.Single();
            Assert.That(_client.AddCalls, Is.EqualTo(1));
            Assert.That(entity.RowKey, Is.EqualTo(id));
            Assert.That(entity.ApplicationName, Is.EqualTo(_log.ApplicationName));
            Assert.That(entity.PartitionKey, Is.EqualTo(AzureHelper.EncodeAzureKey(_log.ApplicationName)));
            Assert.That(entity.HostName, Is.EqualTo(error.HostName));
            Assert.That(entity.Message, Is.EqualTo(error.Message));
            Assert.That(entity.Source, Is.EqualTo(error.Source));
            Assert.That(entity.Type, Is.EqualTo(error.Type));
            Assert.That(entity.User, Is.EqualTo(error.User));
            Assert.That(entity.StatusCode, Is.EqualTo(error.StatusCode));
            Assert.That(ErrorXml.DecodeString(entity.AllXml).Message, Is.EqualTo(error.Message));
            Assert.That(_log.GetError(id).Error.Message, Is.EqualTo(error.Message));
        }

        [Test]
        public void LogRejectsNullAndPropagatesWriteFailures()
        {
            Assert.Throws<ArgumentNullException>(() => _log.Log(null));
            _client.AddFailure = new RequestFailedException(500, "Write failed");
            Assert.Throws<RequestFailedException>(() => _log.Log(new Error(new Exception("test"))));
            Assert.That(_client.Entities, Is.Empty);
        }

        [TestCase(0, 2, 7, 0, 2, 3)]
        [TestCase(1, 2, 7, 2, 2, 5)]
        [TestCase(2, 2, 5, 4, 1, 5)]
        [TestCase(1, 2, 4, 2, 2, 4)]
        [TestCase(3, 2, 4, 0, 0, 4)]
        [TestCase(0, 2, 0, 0, 0, 0)]
        public void GetErrorsReturnsOnlyRequestedPageAndCorrectCount(
            int page, int size, int total, int first, int count, int reported)
        {
            Seed(total);
            var entries = new ArrayList();
            Assert.That(_log.GetErrors(page, size, entries), Is.EqualTo(reported));
            Assert.That(entries.Count, Is.EqualTo(count));
            Assert.That(entries.Cast<ErrorLogEntry>().Select(e => e.Error.Message),
                Is.EqualTo(Enumerable.Range(first, count).Select(i => "error-" + i)));
        }

        [Test]
        public void GetErrorsStopsAfterOneLookaheadAndIgnoresOtherApplications()
        {
            Seed(50);
            Seed(10, "other-app");
            var entries = new ArrayList();
            Assert.That(_log.GetErrors(1, 2, entries), Is.EqualTo(5));
            Assert.That(_client.QueryReadCount, Is.EqualTo(5));
            Assert.That(_client.RequestedPageSize, Is.EqualTo(2));
            Assert.That(entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetErrorsDoesNotIncludePreexistingListItemsInCount()
        {
            Seed(2);
            var entries = new ArrayList { "existing" };
            Assert.That(_log.GetErrors(0, 2, entries), Is.EqualTo(2));
            Assert.That(entries.Count, Is.EqualTo(3));
        }

        [Test]
        public void GetErrorsSupportsNullListWithoutDecodingXml()
        {
            Seed(5);
            foreach (var entity in _client.Entities) entity.AllXml = "not xml";
            Assert.That(_log.GetErrors(0, 2, null), Is.EqualTo(3));
        }

        [Test]
        public void GetErrorsDoesNotDecodeSkippedOrLookaheadRecords()
        {
            Seed(6);
            _client.Entities[0].AllXml = "invalid";
            _client.Entities[1].AllXml = "invalid";
            _client.Entities[4].AllXml = "invalid";
            Assert.That(_log.GetErrors(1, 2, new ArrayList()), Is.EqualTo(5));
        }

        [Test]
        public void ZeroPageSizeReturnsZeroWithoutQuerying()
        {
            Assert.That(_log.GetErrors(int.MaxValue, 0, null), Is.Zero);
            Assert.That(_client.QueryCalls, Is.Zero);
        }

        [TestCase(-1, 1)]
        [TestCase(0, -1)]
        [TestCase(int.MaxValue, 2)]
        public void InvalidPagesAreRejectedWithoutQuerying(int index, int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _log.GetErrors(index, size, new ArrayList()));
            Assert.That(_client.QueryCalls, Is.Zero);
        }

        [Test]
        public void QueryFailuresArePropagated()
        {
            _client.QueryFailure = new RequestFailedException(503, "Unavailable");
            Assert.Throws<RequestFailedException>(() => _log.GetErrors(0, 2, new ArrayList()));
        }

        [Test]
        public void GetErrorUsesBothKeysAndReturnsNullWhenAbsent()
        {
            Seed(1, "other-app");
            var id = _client.Entities.Single().RowKey;
            Assert.That(_log.GetError(id), Is.Null);
            Assert.That(_client.LookupPartition, Is.EqualTo(AzureHelper.EncodeAzureKey("test-app")));
            Assert.That(_client.LookupRow, Is.EqualTo(id));
            Assert.That(_log.GetError("missing"), Is.Null);
            Seed(1);
            Assert.That(_log.GetError(id).Error.Message, Is.EqualTo("error-0"));
            Assert.That(_client.QueryCalls, Is.Zero);
        }

        [Test]
        public void GetErrorRejectsNullAndEmptyIdsAndPropagatesFailures()
        {
            Assert.Throws<ArgumentNullException>(() => _log.GetError(null));
            Assert.Throws<ArgumentException>(() => _log.GetError(""));
            _client.LookupFailure = new RequestFailedException(403, "Denied");
            Assert.Throws<RequestFailedException>(() => _log.GetError("id"));
        }

        [Test]
        public void EmptyApplicationHasUsablePartitionAndCanLogAndRetrieve()
        {
            var log = new AzureTableStorageErrorLog(Config(""), (c, t) => _client);
            var id = log.Log(new Error(new Exception("empty app")));
            Assert.That(_client.Entities.Single().PartitionKey, Is.EqualTo(""));
            Assert.That(log.GetError(id).Error.Message, Is.EqualTo("empty app"));
            Assert.That(log.GetErrors(0, 2, new ArrayList()), Is.EqualTo(1));
        }
    }
}
