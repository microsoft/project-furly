// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.CosmosDb.Clients
{
    using Furly.Extensions.Storage;
    using Autofac;
    using AutoFixture;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Categories;

    [SystemTest]
    public class CosmosDbServiceQueryTests : IClassFixture<CosmosDbServiceClientFixture>
    {
        private readonly CosmosDbServiceClientFixture _fixture;

        public CosmosDbServiceQueryTests(CosmosDbServiceClientFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task QueryAllDocuments1Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var query = documents.CreateQuery<dynamic>();
            var results = await RunAsync(query).ConfigureAwait(false);
            Assert.Equal(2, results.Count);
        }

        [SkippableFact]
        public async Task QueryAllDocuments2Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families =
                from f in documents.CreateQuery<Family>()
                select f;
            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);
        }

        [SkippableFact]
        public async Task QueryAllDocuments3Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>();
            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);
        }

        [SkippableFact]
        public async Task QueryAndersonAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>(1)
                .Where(d => d.LastName == "Andersen");

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
            var family = results.Single().Value;
            Assert.Single(family.Children!);
#pragma warning disable RCS1077 // Optimize LINQ method call
            Assert.Equal(1, family.Children!.Select(c => c.Pets!.Length).Sum());
#pragma warning restore RCS1077 // Optimize LINQ method call
        }

        [SkippableFact]
        public async Task QueryWithAndFilterAndProjectionAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var query =
                from f in documents.CreateQuery<Family>()
                where f.Id == "AndersenFamily" || f.Address!.City == "NY"
                select new { Name = f.LastName, f.Address!.City };

            var results1 = await RunAsync(query).ConfigureAwait(false);
            Assert.Equal(2, results1.Count);

            var query2 = documents.CreateQuery<Family>(1)
                .Where(d => d.LastName == "Andersen")
                .Select(f => new { Name = f.LastName });

            var results2 = await RunAsync(query2).ConfigureAwait(false);
            Assert.Single(results2);

            query = documents.CreateQuery<Family>()
                       .Where(f => f.Id == "AndersenFamily" || f.Address!.City == "NY")
                       .Select(f => new { Name = f.LastName, f.Address!.City });

            results1 = await RunAsync(query).ConfigureAwait(false);
            Assert.Equal(2, results1.Count);
        }

        [SkippableFact]
        public async Task QueryWithUnaryAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = from f in documents.CreateQuery<Family>()
                           where !f.IsRegistered
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => !f.IsRegistered);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.IsRegistered);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.IsRegistered && f.Id == "AndersenFamily");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => !f.IsRegistered && f.Id != "AndersenFamily");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => !f.IsRegistered)
                .Where(f => f.Id != "AndersenFamily");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => !f.IsRegistered || f.Id == "AndersenFamily");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);

            families = documents.CreateQuery<Family>()
                .Where(f => !(f.IsRegistered && f.Id != "AndersenFamily"));

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);
        }

        [SkippableFact]
        public async Task QueryWithAndFilterAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = from f in documents.CreateQuery<Family>()
                           where f.Id == "AndersenFamily" && f.Address!.City == "Seattle"
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Id == "AndersenFamily" && f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Id == "AndersenFamily")
                .Where(f => f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithArrayLengthCompareAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(true, "Not supported yet");

            var families = from f in documents.CreateQuery<Family>()
                           where f.Children!.Length == 2
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Children!.Length == 1);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Children!.Length == 1)
                .Where(f => f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithModuloCompareAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>()
                .Where(f => (f.Address!.Zip % 2) == 0);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => (f.Address!.Zip % 2) < 1);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithListCount1Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(true, "Not supported yet");

            var families = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Count == 2);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithListCount2Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(true, "Not supported yet");

            var families = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Count > 2);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Count > 2)
                .Where(f => f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithArrayContains1Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = from f in documents.CreateQuery<Family>()
                           where f.Colors!.Contains("blue")
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);

            families = from f in documents.CreateQuery<Family>()
                       where f.Colors!.Contains("blue") && f.Colors!.Contains("yellow")
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Contains("blue"))
                .Where(f => f.Colors!.Contains("yellow"));

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Contains("blue"))
                .Where(f => f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithArrayContains2Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families1 = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Any(elem => elem == "red" || elem == "yellow" || elem == "blue"));

            var results1 = await RunAsync(families1).ConfigureAwait(false);
            Assert.Equal(2, results1.Count);

            var families2 = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Any(elem => elem == "red" || elem == "yellow"));

            var results2 = await RunAsync(families2).ConfigureAwait(false);
            Assert.Equal(2, results2.Count);
        }

        [SkippableFact]
        public async Task QueryWithArrayContains3Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families1 = documents.CreateQuery<Family>()
                .Where(f => f.Colors!.Contains("red") || f.Colors!.Contains("yellow"));

            var results1 = await RunAsync(families1).ConfigureAwait(false);
            Assert.Equal(2, results1.Count);
        }

        [SkippableFact]
        public async Task QueryWithAndAndOrFilterAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = from f in documents.CreateQuery<Family>()
                           where (f.Id == "AndersenFamily" || f.Id == "WakefieldFamily")
                            && f.Address!.City == "Seattle"
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => (f.Id == "AndersenFamily" || f.Id == "WakefieldFamily")
                    && f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Id == "AndersenFamily" || f.Id == "WakefieldFamily")
                .Where(f => f.Address!.City == "Seattle");

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where (f.Id == "AndersenFamily" || f.Id == "WakefieldFamily") &&
                        (f.Address!.City == "Seattle" || f.Address!.Zip > 0)
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);

            families = documents.CreateQuery<Family>()
                .Where(f => (f.Id == "AndersenFamily" || f.Id == "WakefieldFamily") &&
                            (f.Address!.City == "Seattle" || f.Address!.Zip > 0));

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Id == "AndersenFamily" || f.Id == "WakefieldFamily")
                .Where(f => f.Address!.City == "Seattle" || f.Address!.Zip != 0.6f);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);

            families = documents.CreateQuery<Family>()
                .Where(f => f.Id == "AndersenFamily" || f.Id == "WakefieldFamily")
                .Where(f => !(f.Address!.City == "Seattle" || f.Address!.Zip == 98103));

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithEqualsOnIdAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families =
                from f in documents.CreateQuery<Family>()
                where f.Id == "AndersenFamily"
                select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>().Where(f => f.Id == "AndersenFamily");
            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithInequalityAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var query = from f in documents.CreateQuery<Family>()
                        where f.Id != "AndersenFamily"
                        select f;

            var results = await RunAsync(query).ConfigureAwait(false);
            Assert.Single(results);

            query = documents.CreateQuery<Family>()
                       .Where(f => f.Id != "AndersenFamily");

            results = await RunAsync(query).ConfigureAwait(false);
            Assert.Single(results);

            query =
                from f in documents.CreateQuery<Family>()
                where f.Id == "Wakefield" && f.Address!.City != "NY"
                select f;

            results = await RunAsync(query).ConfigureAwait(false);
            Assert.Empty(results);
        }

        [SkippableFact]
        public async Task QueryWithRangeOperatorsOnNumbers1Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var families = from f in documents.CreateQuery<Family>()
                           where f.Address!.Zip > 20000
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip > 20000);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Zip >= 98103
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip >= 98103);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Zip < 98103
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip < 98103);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Zip <= 20000
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip <= 20000);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithRangeOperatorsOnNumbers2Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var families = from f in documents.CreateQuery<Family>()
                           where f.Address!.Size > 5.82
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Size > 5.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Size >= 15.82
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Size >= 15.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Size < 15.82
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Size < 15.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Size <= 5.82
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Size <= 5.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithRangeOperatorsOnNumbers3Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var families = from f in documents.CreateQuery<Family>()
                           where f.Address!.Zip > 20000 && f.Address!.Size > 5.82
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip > 20000)
                       .Where(f => f.Address!.Size > 5.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Zip >= 98103 && f.Address!.Size > 5.82
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip >= 98103)
                       .Where(f => f.Address!.Size > 5.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Zip < 98103 && f.Address!.Size == 5.82
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip < 98103)
                       .Where(f => f.Address!.Size == 5.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Address!.Zip <= 20000 && f.Address!.Size != 7.82
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Address!.Zip <= 20000)
                       .Where(f => f.Address!.Size != 7.82);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithRangeOperatorsOnNumbers4Async()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var families = from f in documents.CreateQuery<Family>()
                           where f.Children![0].Grade > 5
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Children![0].Grade > 5);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Children![0].Grade >= 8
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Children![0].Grade >= 8);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Children![0].Grade <= 5
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Children![0].Grade <= 5);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = from f in documents.CreateQuery<Family>()
                       where f.Children![0].Grade < 8
                       select f;

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.Children![0].Grade < 8);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithRangeOperatorsOnStringsAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>()
                .Where(f => f.Address!.State!.Equals("NY", StringComparison.OrdinalIgnoreCase));

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithRangeOperatorsDateTimesAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var date = DateTime.UtcNow.AddDays(-3);
            var families = documents.CreateQuery<Family>()
                .Where(f => f.RegistrationDate >= date);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithOrderByDateTimesAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>()
                .OrderBy(f => f.RegistrationDate);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);
            Assert.Equal("WakefieldFamily", results[0].Id);
        }

        [SkippableFact]
        public async Task QueryWithOrderByDescendingDateTimesAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>()
                .OrderByDescending(f => f.RegistrationDate);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Equal(2, results.Count);
            Assert.Equal("AndersenFamily", results[0].Id);
        }

        [SkippableFact]
        public async Task QueryWithOrderByDateTimesLimitAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>()
                .OrderBy(f => f.RegistrationDate)
                .Take(1);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
            Assert.Equal("WakefieldFamily", results.Single().Id);
        }

        [SkippableFact]
        public async Task QueryWithOrderByDescendingDateTimesLimitAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var families = documents.CreateQuery<Family>()
                .OrderByDescending(f => f.RegistrationDate)
                .Take(1);

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
            Assert.Equal("AndersenFamily", results.Single().Id);
        }

        [SkippableFact]
        public async Task QueryWithOrderByNumbersAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var families =
                from f in documents.CreateQuery<Family>()
                where f.LastName == "Andersen"
                orderby f.Children![0].Grade
                select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            // LINQ Lambda
            families = documents.CreateQuery<Family>()
                .Where(f => f.LastName == "Andersen")
                .OrderBy(f => f.Children![0].Grade);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithOrderByStringsAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);
            var families = from f in documents.CreateQuery<Family>()
                           where f.LastName == "Andersen"
                           orderby f.Address!.State descending
                           select f;

            var results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);

            families = documents.CreateQuery<Family>()
                       .Where(f => f.LastName == "Andersen")
                       .OrderByDescending(f => f.Address!.State);

            results = await RunAsync(families).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithCountAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var count = await documents.CreateQuery<Family>()
                .Where(f => f.LastName == "Andersen")
                .CountAsync().ConfigureAwait(false);

            Assert.Equal(1, count);

            count = await documents.CreateQuery<Family>()
                .SelectMany(f => f.Children!)
                .CountAsync().ConfigureAwait(false);

            Assert.Equal(3, count);
        }

        [SkippableFact]
        public async Task QueryWithSubdocumentsAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var children = documents.CreateQuery<Family>()
                .SelectMany(family => family.Children!.Select(c => c));

            var results = await RunAsync(children).ConfigureAwait(false);
            Assert.Equal(3, results.Count);
        }

        [SkippableFact]
        public async Task QueryWithTwoJoinsAndFilterAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var query = documents.CreateQuery<Family>().Where(family => family.Children! != null)
                    .SelectMany(family => family.Children!.Where(child => child.Pets != null)
                    .SelectMany(child => child.Pets!
                    .Where(pet => pet.GivenName == "Fluffy")
                    .Select(pet => new
                    {
                        family = family.Id,
                        child = child.FirstName,
                        pet = pet.GivenName
                    }
                    )));

            var results = await RunAsync(query).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithTwoJoinsAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var query = documents.CreateQuery<Family>().Where(family => family.Children! != null)
                    .SelectMany(family => family.Children!.Where(child => child.Pets != null)
                    .SelectMany(child => child.Pets!
                    .Select(pet => new
                    {
                        family = family.Id,
                        child = child.FirstName,
                        pet = pet.GivenName
                    }
                    )));

            var results = await RunAsync(query).ConfigureAwait(false);
            Assert.Equal(3, results.Count);
        }

        [SkippableFact]
        public async Task QueryWithSingleJoinAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var query = documents.CreateQuery<Family>()
                    .SelectMany(family => family.Children!
                    .Select(_ => family.Id));

            var results = await RunAsync(query).ConfigureAwait(false);
            Assert.Equal(3, results.Count);
        }

        [SkippableFact]
        public async Task QueryWithStringStartsWithAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(true, "Not supported yet"); // TODO

            var query = documents.CreateQuery<Family>()
                .Where(family => family.LastName != null)
                .Where(family => family.LastName!.StartsWith("An", StringComparison.Ordinal));
            var results = await RunAsync(query).ConfigureAwait(false);
            Assert.Single(results);
        }

        [SkippableFact]
        public async Task QueryWithMathAndArrayOperatorsAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            var query2 = documents.CreateQuery<Family>()
                .Select(family => (int)Math.Round((double)family.Children![0].Grade));

            var results2 = await RunAsync(query2).ConfigureAwait(false);
            Assert.Collection(results2, a => Assert.Equal(5, a.Value), a => Assert.Equal(8, a.Value));

            var query3 = documents.CreateQuery<Family>()
#pragma warning disable RCS1077 // Optimize LINQ method call.
                .Select(family => family.Children!.Count());
#pragma warning restore RCS1077 // Optimize LINQ method call.
            var results3 = await RunAsync(query3).ConfigureAwait(false);
            Assert.Collection(results3, a => Assert.Equal(1, a.Value), a => Assert.Equal(2, a.Value));
        }

        [SkippableFact]
        public async Task QueryWithDistinct1Async()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                var now = DateTime.UtcNow;
                foreach (var f in new Fixture().CreateMany<Family>(20))
                {
                    f.LastName = "Same";
                    f.RegistrationDate = now;
                    f.IsRegistered = true;
                    f.Count = 6;
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .Distinct();
                var results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Single(results1);
                Assert.Equal("Same", results1.Single().Value);

                var query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .Distinct();
                var results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Single(results2);
                Assert.Equal(6, results2.Single().Value);

                var query3 = documents.CreateQuery<Family>()
                    .Select(x => x.RegistrationDate)
                    .Distinct();
                var results3 = await RunAsync(query3).ConfigureAwait(false);
                Assert.Single(results3);
                Assert.Equal(now, results3.Single().Value);

                var query4 = documents.CreateQuery<Family>()
                    .Select(x => x.IsRegistered)
                    .Distinct();
                var results4 = await RunAsync(query4).ConfigureAwait(false);
                Assert.Single(results4);
                Assert.True(results4.Single().Value);
            }
        }

        [SkippableFact]
        public async Task QueryWithDistinct2Async()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                var families = new Fixture().CreateMany<Family>(5);
                foreach (var f in families)
                {
                    f.LastName = "Same";
                    f.Count = 6;
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                families = new Fixture().CreateMany<Family>(5);
                foreach (var f in families)
                {
                    f.LastName = "Other";
                    f.Count = null;
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .Distinct();
                var results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(2, results1.Count);

                query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .Distinct()
                    .OrderBy(x => x);
                results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(2, results1.Count);
                Assert.Equal("Other", results1[0].Value);
                query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .Distinct()
                    .OrderByDescending(x => x);
                results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(2, results1.Count);
                Assert.Equal("Same", results1[0].Value);

                var query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .Distinct();
                var results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(2, results2.Count);

                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .Distinct()
                    .OrderBy(x => x);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(2, results2.Count);
                Assert.Null(results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .Distinct()
                    .OrderByDescending(x => x);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(2, results2.Count);
                Assert.Equal(6, results2[0].Value);

                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .Distinct()
                    .OrderBy(x => x)
                    .Take(1);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Single(results2);
                Assert.Null(results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .Distinct()
                    .OrderByDescending(x => x)
                    .Take(1);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Single(results2);
                Assert.Equal(6, results2[0].Value);
            }
        }

        [SkippableFact]
        public async Task QueryWithSelectAndOrderByAsync()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                var count = 0;
                var families = new Fixture().CreateMany<Family>(5);
                foreach (var f in families)
                {
                    f.LastName = "Same";
                    f.Count = ++count;
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                families = new Fixture().CreateMany<Family>(5);
                foreach (var f in families)
                {
                    f.LastName = "Other";
                    f.Count = ++count;
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .OrderBy(x => x);
                var results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(10, results1.Count);
                Assert.Equal("Other", results1[0].Value);
                query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .OrderByDescending(x => x);
                results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(10, results1.Count);
                Assert.Equal("Same", results1[0].Value);
                query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .OrderByDescending(x => x)
                    .Take(1);
                results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Single(results1);
                Assert.Equal("Same", results1[0].Value);
                query1 = documents.CreateQuery<Family>()
                    .Select(x => x.LastName)
                    .OrderBy(x => x)
                    .Take(1);
                results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Single(results1);
                Assert.Equal("Other", results1[0].Value);

                var query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .OrderBy(x => x);
                var results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(10, results2.Count);
                Assert.Equal(1, results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .OrderByDescending(x => x);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(10, results2.Count);
                Assert.Equal(10, results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .OrderByDescending(x => x)
                    .Take(1);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Single(results1);
                Assert.Equal(10, results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .OrderBy(x => x)
                    .Take(2);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(2, results2.Count);
                Assert.Equal(1, results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .OrderBy(x => x)
                    .Take(100);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(10, results2.Count);
                Assert.Equal(1, results2[0].Value);
                query2 = documents.CreateQuery<Family>()
                    .Select(x => x.Count)
                    .OrderBy(x => x)
                    .Where(x => x > 3);
                results2 = await RunAsync(query2).ConfigureAwait(false);
                Assert.Equal(7, results2.Count);
                Assert.Equal(4, results2[0].Value);
            }
        }

        [SkippableFact]
        public async Task QueryContinueTest1Async()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                foreach (var f in new Fixture().CreateMany<Family>(100))
                {
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query1 = documents.CreateQuery<Family>(10);
                var results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(100, results1.Count);

                var query2 = documents.CreateQuery<Family>(10);
                var results2 = query2.GetResults();

                var loops = 0;
                for (var i = 0; i < 20; i++)
                {
                    loops++;
                    if (results2.HasMore())
                    {
                        var result = await results2.ReadAsync().ConfigureAwait(false);
                        Assert.NotNull(result);
                        if (!result.Any())
                        {
                            Assert.Null(results2.ContinuationToken);
                            loops--; // Ok to return empty as last loop
                        }
                        else
                        {
                            Assert.NotEmpty(result);
                            Assert.Equal(10, result.Count());
                        }
                    }
                    if (results2.ContinuationToken == null)
                    {
                        break;
                    }
                    results2 = documents.ContinueQuery<Family>(results2.ContinuationToken, 10);
                }
                Assert.Equal(10, loops);
            }
        }

        [SkippableFact]
        public async Task QueryContinueTest2Async()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                foreach (var f in new Fixture().CreateMany<Family>(100))
                {
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query1 = documents.CreateQuery<Family>(10);
                var results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(100, results1.Count);

                var query2 = documents.CreateQuery<Family>(10);
                var results2 = query2.GetResults();

                var loops = 0;
                for (var i = 0; i < 20; i++)
                {
                    loops++;
                    var result = await results2.ReadAsync().ConfigureAwait(false);
                    Assert.NotNull(result);
                    if (!result.Any())
                    {
                        Assert.Null(results2.ContinuationToken);
                        loops--; // Ok to return empty as last loop
                    }
                    else
                    {
                        Assert.NotEmpty(result);
                        Assert.Equal(10, result.Count());
                    }
                    if (results2.ContinuationToken == null)
                    {
                        result = await results2.ReadAsync().ConfigureAwait(false);
                        Assert.NotNull(result);
                        Assert.Empty(result);
                        break;
                    }
                    results2 = documents.ContinueQuery<Family>(results2.ContinuationToken, 10);
                }
                Assert.Equal(10, loops);
            }
        }

        [SkippableFact]
        public async Task QueryContinueTest3Async()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                foreach (var f in new Fixture().CreateMany<Family>(5))
                {
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query1 = documents.CreateQuery<Family>(10);
                var results1 = await RunAsync(query1).ConfigureAwait(false);
                Assert.Equal(5, results1.Count);

                var query2 = documents.CreateQuery<Family>(10);
                var results2 = query2.GetResults();

                Assert.True(results2.HasMore());
                var result = await results2.ReadAsync().ConfigureAwait(false);
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                Assert.Equal(5, result.Count());

                if (results2.ContinuationToken != null)
                {
                    result = await results2.ReadAsync().ConfigureAwait(false);
                    Assert.NotNull(result);
                    Assert.Empty(result);
                }
                Assert.Null(results2.ContinuationToken);
            }
        }

        [SkippableFact]
        public async Task QueryContinueTest4Async()
        {
            using (var container = await _fixture.GetContainerAsync().ConfigureAwait(false))
            {
                Skip.If(container == null);
                var documents = container.Container;

                foreach (var f in new Fixture().CreateMany<Family>(100))
                {
                    await documents.UpsertAsync(f).ConfigureAwait(false);
                }

                var query2 = documents.CreateQuery<Family>(10);
                var results2 = query2.GetResults();

                Assert.True(results2.HasMore());
                var result = await results2.ReadAsync().ConfigureAwait(false);
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                Assert.Equal(10, result.Count());
                Assert.NotNull(results2.ContinuationToken);

                results2 = documents.ContinueQuery<Family>(results2.ContinuationToken!, 40);
                Assert.True(results2.HasMore());
                result = await results2.ReadAsync().ConfigureAwait(false);
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                Assert.Equal(40, result.Count());
                Assert.NotNull(results2.ContinuationToken);

                results2 = documents.ContinueQuery<Family>(results2.ContinuationToken!);
                Assert.True(results2.HasMore());
                result = await results2.ReadAsync().ConfigureAwait(false);
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                Assert.Equal(50, result.Count());

                if (results2.ContinuationToken != null)
                {
                    result = await results2.ReadAsync().ConfigureAwait(false);
                    Assert.NotNull(result);
                    Assert.Empty(result);
                }
                Assert.Null(results2.ContinuationToken);
            }
        }

        [SkippableFact]
        public async Task QueryContinueBadArgumentsThrowsAsync()
        {
            var documents = await _fixture.GetDocumentsAsync().ConfigureAwait(false);
            Skip.If(documents == null);

            Assert.Throws<ArgumentNullException>(() => documents.ContinueQuery<Family>(null!));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            {
                var results = documents.ContinueQuery<Family>("badtoken");
                return results.ReadAsync();
            }).ConfigureAwait(false);
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            {
                var results = documents.ContinueQuery<Family>("{}");
                return results.ReadAsync();
            }).ConfigureAwait(false);
        }

        private static async Task<List<IDocumentInfo<T>>> RunAsync<T>(IQuery<T> query)
        {
            var feed = query.GetResults();
            var results = new List<IDocumentInfo<T>>();
            while (feed.HasMore())
            {
                var result = await feed.ReadAsync().ConfigureAwait(false);
                results.AddRange(result);
            }
            return results;
        }
    }
}
