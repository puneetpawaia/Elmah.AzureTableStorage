using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace Elmah.AzureTableStorage.Tests
{
    // Implements the Azure SDK's virtual operations in memory. No network access.
    internal sealed class FakeTableClient : TableClient
    {
        public readonly List<ElmahEntity> Entities = new List<ElmahEntity>();
        public int CreateCalls;
        public int AddCalls;
        public int QueryCalls;
        public int QueryReadCount;
        public int? RequestedPageSize;
        public string LookupPartition;
        public string LookupRow;
        public Exception CreateFailure;
        public Exception AddFailure;
        public Exception QueryFailure;
        public Exception LookupFailure;

        public override Response<TableItem> CreateIfNotExists(CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            if (CreateFailure != null) throw CreateFailure;
            return Response.FromValue(new TableItem("Elmah"), new TestResponse());
        }

        public override Response AddEntity<T>(T entity, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            if (AddFailure != null) throw AddFailure;
            var item = (ElmahEntity)(object)entity;
            if (Entities.Any(e => e.PartitionKey == item.PartitionKey && e.RowKey == item.RowKey))
                throw new RequestFailedException(409, "Duplicate entity");
            Entities.Add(item);
            return new TestResponse();
        }

        public override Pageable<T> Query<T>(Expression<Func<T, bool>> filter = null,
            int? maxPerPage = null, IEnumerable<string> select = null, CancellationToken cancellationToken = default)
        {
            QueryCalls++;
            RequestedPageSize = maxPerPage;
            if (QueryFailure != null) throw QueryFailure;
            var predicate = filter?.Compile();
            var values = Entities.OrderBy(e => e.RowKey, StringComparer.Ordinal).Cast<T>()
                .Where(value => predicate == null || predicate(value));
            return new TrackingPageable<T>(values, () => QueryReadCount++);
        }

        public override NullableResponse<T> GetEntityIfExists<T>(string partitionKey, string rowKey,
            IEnumerable<string> select = null, CancellationToken cancellationToken = default)
        {
            LookupPartition = partitionKey;
            LookupRow = rowKey;
            if (LookupFailure != null) throw LookupFailure;
            var match = Entities.FirstOrDefault(e => e.PartitionKey == partitionKey && e.RowKey == rowKey);
            return new TestNullableResponse<T>((T)(object)match);
        }
    }

    internal sealed class TrackingPageable<T> : Pageable<T>
    {
        private readonly IEnumerable<T> _values;
        private readonly Action _read;

        public TrackingPageable(IEnumerable<T> values, Action read)
        {
            _values = values;
            _read = read;
        }

        public override IEnumerator<T> GetEnumerator()
        {
            foreach (var value in _values)
            {
                _read();
                yield return value;
            }
        }

        public override IEnumerable<Page<T>> AsPages(string continuationToken = null, int? pageSizeHint = null)
        {
            yield return Page<T>.FromValues(_values.ToList(), null, new TestResponse());
        }
    }

    internal sealed class TestNullableResponse<T> : NullableResponse<T>
    {
        private readonly T _value;
        public TestNullableResponse(T value) { _value = value; }
        public override bool HasValue => (object)_value != null;
        public override T Value => HasValue ? _value : throw new InvalidOperationException("No value");
        public override Response GetRawResponse() => new TestResponse();
    }

    internal sealed class TestResponse : Response
    {
        public override int Status => 200;
        public override string ReasonPhrase => "OK";
        public override Stream ContentStream { get; set; }
        public override string ClientRequestId { get; set; }
        public override void Dispose() { }
        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => Array.Empty<HttpHeader>();
        protected override bool TryGetHeader(string name, out string value) { value = null; return false; }
        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values) { values = null; return false; }
    }
}
