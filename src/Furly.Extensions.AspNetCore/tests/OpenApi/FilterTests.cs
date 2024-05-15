// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.AspNetCore.Tests.OpenApi
{
    using Furly.Extensions.AspNetCore.Tests.Fixtures;
    using Furly.Extensions.AspNetCore.OpenApi;
    using Furly.Extensions.Serializers;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.OpenApi.Models;
    using Newtonsoft.Json;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Xunit;

    public class FilterTests : IClassFixture<WebAppFixture<FilterStartup>>
    {
        private readonly WebAppFixture<FilterStartup> _factory;

        public FilterTests(WebAppFixture<FilterStartup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TestFilterWithVariantValue()
        {
            var httpClient = _factory.CreateClient();
            var response = await httpClient.GetStringAsync(new Uri("http://localhost/swagger/v2/openapi.json"));

            var expected = """
{
  "swagger": "2.0",
  "info": {
    "title": "Test",
    "description": "TestDescription",
    "contact": {
      "url": "http://test/test"
    },
    "license": {
      "name": "none",
      "url": "http://somelicsense"
    },
    "version": "v2"
  },
  "host": "localhost",
  "schemes": [
    "https",
    "http"
  ],
  "paths": {
    "/test/v2/filter": {
      "post": {
        "tags": [
          "FilterTests"
        ],
        "operationId": "GetTestModel",
        "consumes": [
          "application/json-patch+json",
          "application/json",
          "text/json",
          "application/*+json"
        ],
        "produces": [
          "text/plain",
          "application/json",
          "text/json"
        ],
        "parameters": [
          {
            "in": "body",
            "name": "body",
            "schema": {
              "$ref": "#/definitions/TestModel"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "schema": {
              "$ref": "#/definitions/TestModel"
            }
          }
        }
      }
    }
  },
  "definitions": {
    "TestModel": {
      "required": [
        "VariantValueValue1"
      ],
      "type": "object",
      "properties": {
        "VariantValueValue1": {
          "description": "Represents primitive or structurally complex value",
          "type": "object"
        },
        "VariantValueValue2": {
          "description": "Represents primitive or structurally complex value",
          "type": "object"
        }
      },
      "additionalProperties": false
    }
  }
}
""".Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Equal(expected, response);
        }
    }

    public class FilterStartup
    {
        public string Name { get; set; } = "Test";
        public string Description { get; set; } = "TestDescription";

        /// <summary>
        /// Configure services
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(options => options
                .AddConsole()
                .AddDebug())
                ;
            services.AddHttpClient();

            services.AddControllers()
                .AddNewtonsoftSerializer();

            services.Configure<OpenApiOptions>(o =>
            {
                o.ProjectUri = new Uri("http://test/test");
                o.License = new OpenApiLicense
                {
                    Name = "none",
                    Url = new Uri("http://somelicsense")
                };
            });
            services.AddSwagger(Name, Description);
        }

        /// <summary>
        /// This method is called by the runtime, after the ConfigureServices
        /// method above and used to add middleware
        /// </summary>
        /// <param name="app"></param>
        public static void Configure(IApplicationBuilder app)
        {
            app.UsePathBase();
            app.UseHeaderForwarding();

            app.UseRouting();
            app.UseSwagger();

            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }

    [ApiVersion("2")]
    [Route("test/v{version:apiVersion}/filter")]
    [ApiController]
    public class FilterTestsController : ControllerBase
    {
        [HttpPost]
        public TestModel GetTestModel(TestModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            return _myModel;
        }

        private readonly TestModel _myModel = new();
    }

    [DataContract]
    public record class TestModel
    {
        /// <summary>
        /// First
        /// </summary>
        [DataMember(Name = "VariantValueValue1", Order = 0)]
        [Required]
        public VariantValue VariantValueValue1 { get; set; } = VariantValue.Null;

        /// <summary>
        /// Second
        /// </summary>
        [DataMember(Name = "VariantValueValue2", Order = 2)]
        public VariantValue? VariantValueValue2 { get; set; }
    }
}
