// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.CouchDb.Clients
{
    using Furly.Extensions.CouchDb.Runtime;
    using Furly.Extensions.Storage;
    using Furly.Extensions.Utils;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Xunit.Abstractions;

    public sealed class CouchDbClientFixture : IDisposable
    {
        public CouchDbClientFixture(CouchDbServerFixture server, IMessageSink sink)
        {
            _sink = sink;
            _server = server;
            _query = new Lazy<Task<ContainerWrapper?>>(() => CreateQueryContainerAsync());
        }

        /// <summary>
        /// Get collection interface
        /// </summary>
        public async Task<IDocumentCollection?> GetDocumentsAsync()
        {
            var query = await _query.Value.ConfigureAwait(false);
            return query?.Container;
        }

        /// <summary>
        /// Get collection interface
        /// </summary>
        /// <param name="name"></param>
        public async Task<ContainerWrapper?> GetContainerAsync(string? name = null)
        {
            if (!_server.Up)
            {
                return null;
            }
            var database = await GetDatabaseAsync().ConfigureAwait(false);
            if (name == null)
            {
                name = "";
                for (var i = 0; i < 30; i++)
                {
#pragma warning disable CA5394 // Do not use insecure randomness
                    name += (char)Random.Shared.Next('a', 'z');
#pragma warning restore CA5394 // Do not use insecure randomness
                }
            }
            var docs = await database.OpenContainerAsync(name).ConfigureAwait(false);
            return new ContainerWrapper(database, docs);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_query.IsValueCreated)
            {
                _query.Value.Result?.Dispose();
            }
        }

        /// <summary>
        /// Get collection interface
        /// </summary>
        public async Task<ContainerWrapper?> CreateQueryContainerAsync()
        {
            var query = await GetContainerAsync("test").ConfigureAwait(false);
            if (query == null)
            {
                return null;
            }
            await CreateDocumentsAsync(query.Container).ConfigureAwait(false);
            return query;
        }

        /// <summary>
        /// Creates the documents used in this Sample
        /// </summary>
        /// <param name="collection">collection</param>
        /// <returns>None</returns>
        private static async Task CreateDocumentsAsync(IDocumentCollection collection)
        {
            var AndersonFamily = new Family
            {
                Id = "AndersenFamily",
                LastName = "Andersen",
                Parents = new List<Parent> {
                    new Parent { FirstName = "Thomas" },
                    new Parent { FirstName = "Mary Kay" }
                },
                Children = new Child[] {
                    new Child {
                        FirstName = "Henriette Thaulow",
                        Gender = "female",
                        Grade = 5,
                        Pets = new[]
                        {
                            new Pet { GivenName = "Fluffy" }
                        }
                    }
                },
                Address = new Address
                {
                    State = "WA",
                    County = "King",
                    Zip = 98103,
                    Size = 15.82,
                    City = "Seattle"
                },
                Colors = new List<string> { "yellow", "blue", "orange" },
                IsRegistered = true,
                ExistsFor = TimeSpan.FromMinutes(1),
                RegistrationDate = DateTime.UtcNow.AddDays(-1)
            };

            await collection.UpsertAsync(AndersonFamily).ConfigureAwait(false);

            var WakefieldFamily = new Family
            {
                Id = "WakefieldFamily",
                LastName = "Wakefield",
                Parents = new List<Parent> {
                    new Parent { FamilyName= "Wakefield", FirstName= "Robin" },
                    new Parent { FamilyName= "Miller", FirstName= "Ben" }
                },
                Children = new Child[] {
                    new Child
                    {
                        FamilyName= "Merriam",
                        FirstName= "Jesse",
                        Gender= "female",
                        Grade= 8,
                        Pets= new Pet[] {
                            new Pet { GivenName= "Goofy" },
                            new Pet { GivenName= "Shadow" }
                        }
                    },
                    new Child
                    {
                        FirstName= "Lisa",
                        Gender= "female",
                        Grade= 1
                    }
                },
                Address = new Address
                {
                    State = "NY",
                    County = "Manhattan",
                    Zip = 10592,
                    Size = 5.82,
                    City = "NY"
                },
                Colors = new List<string> { "blue", "red" },
                IsRegistered = false,
                ExistsFor = TimeSpan.FromMinutes(2),
                RegistrationDate = DateTime.UtcNow.AddDays(-30)
            };

            await collection.UpsertAsync(WakefieldFamily).ConfigureAwait(false);
        }

        /// <summary>
        /// Get database
        /// </summary>
        private async Task<IDatabase> GetDatabaseAsync()
        {
            using var config = new ConfigurationManager();
            var server = new CouchDbClient(
                new CouchDbConfig(config).ToOptions(), _sink.ToLogger<CouchDbClient>());
            return await server.OpenAsync("test").ConfigureAwait(false);
        }

        private readonly CouchDbServerFixture _server;
        private readonly Lazy<Task<ContainerWrapper?>> _query;
        private readonly IMessageSink _sink;
    }

    public sealed class ContainerWrapper : IDisposable
    {
        private readonly IDatabase _database;

        public IDocumentCollection Container { get; }

        public ContainerWrapper(IDatabase database, IDocumentCollection container)
        {
            _database = database;
            Container = container;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Try.Op(() => _database.DeleteContainerAsync(Container.Name)
                .GetAwaiter().GetResult());
        }
    }

    [DataContract]
    public sealed class Parent
    {
        [DataMember]
        public string? FamilyName { get; set; }
        [DataMember]
        public string? FirstName { get; set; }
        [DataMember]
        public DateTimeOffset Dob { get; set; }
        [DataMember]
        public int Age { get; set; }
    }

    [DataContract]
    public sealed class Child
    {
        [DataMember]
        public string? FamilyName { get; set; }
        [DataMember]
        public string? FirstName { get; set; }
        [DataMember]
        public string? Gender { get; set; }
        [DataMember]
        public int Grade { get; set; }
        [DataMember]
        public Pet[]? Pets { get; set; }
        [DataMember]
        public DateTime? Dob { get; set; }
    }

    [DataContract]
    public sealed class Pet
    {
        [DataMember]
        public string? GivenName { get; set; }
        [DataMember]
        public DateTimeOffset? Dob { get; set; }
    }

    [DataContract]
    public sealed class Address
    {
        [DataMember]
        public string? State { get; set; }
        [DataMember]
        public string? County { get; set; }
        [DataMember]
        public string? City { get; set; }
        [DataMember]
        public int Zip { get; set; }
        [DataMember]
        public double Size { get; set; }
        [DataMember]
        public TimeSpan? LivedAt { get; set; }
    }

    [DataContract]
    public sealed class Family
    {
        [DataMember(Name = "id")]
        public string? Id { get; set; }
        [DataMember]
        public string? LastName { get; set; }
        [DataMember]
#pragma warning disable CA1002 // Do not expose generic lists
        public List<Parent>? Parents { get; set; }
#pragma warning restore CA1002 // Do not expose generic lists
        [DataMember]
        public Child[]? Children { get; set; }
        [DataMember]
        public Address? Address { get; set; }
        [DataMember]
        public bool IsRegistered { get; set; }
        [DataMember]
        public DateTime RegistrationDate { get; set; }
        [DataMember]
        public TimeSpan ExistsFor { get; set; }
        [DataMember]
#pragma warning disable CA1002 // Do not expose generic lists
        public List<string>? Colors { get; set; }
#pragma warning restore CA1002 // Do not expose generic lists
        [DataMember]
        public int? Count { get; set; }

        public int Ignored { get; set; }
    }
}
