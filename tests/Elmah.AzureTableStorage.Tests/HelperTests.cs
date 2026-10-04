using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Azure;
using NUnit.Framework;

namespace Elmah.AzureTableStorage.Tests
{
    [TestFixture]
    public class HelperTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("normal")]
        [TestCase("app_name")]
        [TestCase("\uffff/\\#?\u0000\u007f")]
        [TestCase("日本語")]
        public void KeyEncodingRoundTripsAndProducesSafeKeys(string value)
        {
            var encoded = AzureHelper.EncodeAzureKey(value);
            Assert.That(encoded, Does.Not.Match(@"[/\\#?\x00-\x1f\x7f-\x9f]"));
            Assert.That(AzureHelper.DecodeAzureKey(encoded), Is.EqualTo(value ?? ""));
        }

        [Test]
        public void KeyDecodingHandlesEmptyInputAndRejectsInvalidBase64()
        {
            Assert.That(AzureHelper.DecodeAzureKey(null), Is.EqualTo(""));
            Assert.Throws<FormatException>(() => AzureHelper.DecodeAzureKey("invalid!"));
        }

        [Test]
        public void DictionaryFindHandlesMissingAndNullValuesAndRejectsInvalidInputs()
        {
            var values = new Hashtable { ["present"] = 7, ["null"] = null, ["wrong"] = "text" };
            Assert.That(values.Find("present", 0), Is.EqualTo(7));
            Assert.That(values.Find("missing", 3), Is.EqualTo(3));
            Assert.That(values.Find("null", 4), Is.EqualTo(4));
            Assert.Throws<ArgumentNullException>(() => ElmahHelper.Find<string>(null, "key", ""));
            Assert.Throws<InvalidCastException>(() => values.Find("wrong", 1));
        }

        [Test]
        public void ConnectionStringResolutionUsesNamedThenDirectThenApplicationSetting()
        {
            var values = new Hashtable
            {
                ["connectionStringName"] = "NamedStorage",
                ["connectionString"] = "direct",
                ["connectionStringAppKey"] = "StorageSetting"
            };
            Assert.That(ElmahHelper.GetConnectionString(values), Is.EqualTo("named-setting"));
            values.Remove("connectionStringName");
            Assert.That(ElmahHelper.GetConnectionString(values), Is.EqualTo("direct"));
            values.Remove("connectionString");
            Assert.That(ElmahHelper.GetConnectionString(values), Is.EqualTo("app-setting"));
        }

        [Test]
        public void MissingConfigurationValuesReturnEmptyStrings()
        {
            Assert.That(ElmahHelper.GetConnectionString(new Hashtable()), Is.EqualTo(""));
            Assert.That(ElmahHelper.GetConnectionString(new Hashtable { ["connectionStringName"] = "absent" }), Is.EqualTo(""));
            Assert.That(ElmahHelper.GetConnectionString(new Hashtable { ["connectionStringAppKey"] = "absent" }), Is.EqualTo(""));
            Assert.Throws<ArgumentNullException>(() => ElmahHelper.GetConnectionString(null));
        }

        [Test]
        public void EntityKeysAreUniqueAndChronologicallySortable()
        {
            var entities = Enumerable.Range(0, 1000).Select(_ => new ElmahEntity("app")).ToArray();
            Assert.That(entities.Select(e => e.RowKey).Distinct().Count(), Is.EqualTo(1000));
            Assert.That(string.CompareOrdinal(entities[0].RowKey.Substring(0, 19), entities.Last().RowKey.Substring(0, 19)),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(entities.All(e => e.PartitionKey == AzureHelper.EncodeAzureKey("app")), Is.True);
        }

        [Test]
        public void EntitySupportsAzureSdkPropertiesAndEmptyApplication()
        {
            var empty = new ElmahEntity(null);
            Assert.That(empty.ApplicationName, Is.EqualTo(""));
            Assert.That(empty.PartitionKey, Is.EqualTo(""));
            var timestamp = DateTimeOffset.UtcNow;
            var entity = new ElmahEntity { PartitionKey = "partition", RowKey = "row", Timestamp = timestamp, ETag = new ETag("tag") };
            Assert.That(entity.PartitionKey, Is.EqualTo("partition"));
            Assert.That(entity.RowKey, Is.EqualTo("row"));
            Assert.That(entity.Timestamp, Is.EqualTo(timestamp));
            Assert.That(entity.ETag, Is.EqualTo(new ETag("tag")));
        }

        [Test]
        public void RowKeysUseAsciiDigitsRegardlessOfCurrentCulture()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(new ElmahEntity("app").RowKey, Does.Match(@"^[0-9]{19}_[a-f0-9]{32}$"));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }
    }
}
