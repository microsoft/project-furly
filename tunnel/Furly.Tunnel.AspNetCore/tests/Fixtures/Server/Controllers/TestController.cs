// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.AspNetCore.Tests.Server.Controllers
{
    using Furly.Tunnel.AspNetCore.Tests.Server.Filters;
    using Furly.Tunnel.AspNetCore.Tests.Server.Models;
    using Furly.Exceptions;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;

    /// <summary>
    /// Test controller
    /// </summary>
    [ApiVersion("1")]
    [ApiVersion("2")]
    [ApiVersion("3")]
    [ApiVersion("4")]
    [Route("v{version:apiVersion}/path/test")]
    [ExceptionsFilter]
    [ApiController]
    public class TestController : ControllerBase
    {
        public static ConcurrentDictionary<string, TestResponseModel> State { get; } =
            new ConcurrentDictionary<string, TestResponseModel>();

        [HttpPost("{id}")]
        public Task<TestResponseModel> TestPostAsync(
            string id, [FromBody][Required] TestRequestModel request)
        {
            ArgumentNullException.ThrowIfNull(id);
            var s = new TestResponseModel
            {
                Input = request.Input,
                Method = "Post",
                Id = id,
            };
            State.AddOrUpdate(s.Id, s, (_, _) => s);
            return Task.FromResult(s);
        }

        [HttpPatch("{id}")]
        public Task TestPatchAsync(
            string id, [FromBody][Required] TestRequestModel request)
        {
            ArgumentNullException.ThrowIfNull(id);
            var s = new TestResponseModel
            {
                Input = request.Input,
                Method = "Patch",
                Id = id,
            };
            State.AddOrUpdate(id, s, (_, _) => s);
            return Task.CompletedTask;
        }

        [HttpPut]
        public Task<TestResponseModel> TestPutAsync(
            [FromBody][Required] TestRequestModel request)
        {
            var s = new TestResponseModel
            {
                Input = request.Input,
                Method = "Put",
                Id = Guid.NewGuid().ToString(),
            };
            State.AddOrUpdate(s.Id, s, (_, _) => s);
            return Task.FromResult(s);
        }

        [HttpGet("{id}")]
        public Task<TestResponseModel> TestGetAsync(
            string id, [FromQuery] string? input)
        {
            ArgumentNullException.ThrowIfNull(id);
            if (!State.TryGetValue(id, out var s))
            {
                s = new TestResponseModel
                {
                    Input = input,
                    Method = "Get",
                    Id = id,
                };
            }
            return Task.FromResult(s);
        }

        [HttpDelete("{id}")]
        public Task TestDeleteAsync(string id)
        {
            ArgumentNullException.ThrowIfNull(id);
            return !State.TryRemove(id, out _) ?
                throw new ResourceNotFoundException("Not found")
                    : Task.CompletedTask;
        }
    }
}
