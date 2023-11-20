// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Mvc;
    using Furly.Extensions.AspNetCore.Serializers;
    using Furly.Extensions.Serializers;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Mvc setup extensions
    /// </summary>
    public static class MvcBuilderEx
    {
        /// <summary>
        /// Add MessagePack serializer
        /// </summary>
        /// <param name="builder"></param>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <c>null</c>.</exception>
        public static IMvcBuilder AddMessagePackSerializer(this IMvcBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            // Add all other serializers
            builder.Services.AddTransient<IConfigureOptions<MvcOptions>>(services =>
                  new ConfigureNamedOptions<MvcOptions>(Options.DefaultName, option =>
                  {
                      var serializers = services.GetService<IEnumerable<IBinarySerializer>>();
                      if (serializers == null)
                      {
                          return;
                      }
                      foreach (var serializer in serializers)
                      {
                          option.OutputFormatters.Add(new SerializerOutputFormatter(serializer));
                          option.InputFormatters.Add(new SerializerInputFormatter(serializer));
                      }
                  }));
            return builder;
        }

        /// <summary>
        /// Add json serializer
        /// </summary>
        /// <param name="builder"></param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="builder"/> is <c>null</c>.</exception>
        public static IMvcBuilder AddJsonSerializer(this IMvcBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Configure json serializer settings transiently to pick up all converters
            builder.Services.AddTransient<IConfigureOptions<JsonOptions>>(services =>
                  new ConfigureNamedOptions<JsonOptions>(Options.DefaultName, options =>
                  {
                      var provider = services.GetService<IJsonSerializerSettingsProvider>();
                      var settings = provider?.Settings;
                      if (settings == null)
                      {
                          return;
                      }

                      options.JsonSerializerOptions.NumberHandling = settings.NumberHandling;
                      options.JsonSerializerOptions.DefaultIgnoreCondition = settings.DefaultIgnoreCondition;
                      options.JsonSerializerOptions.DefaultBufferSize = settings.DefaultBufferSize;
                      options.JsonSerializerOptions.PropertyNamingPolicy = settings.PropertyNamingPolicy;
                      options.JsonSerializerOptions.PropertyNameCaseInsensitive = settings.PropertyNameCaseInsensitive;
                      options.JsonSerializerOptions.IncludeFields = settings.IncludeFields;
                      options.JsonSerializerOptions.UnknownTypeHandling = settings.UnknownTypeHandling;
                      options.JsonSerializerOptions.WriteIndented = settings.WriteIndented;
                      options.JsonSerializerOptions.DictionaryKeyPolicy = settings.DictionaryKeyPolicy;
                      options.JsonSerializerOptions.IgnoreReadOnlyProperties = settings.IgnoreReadOnlyProperties;
                      options.JsonSerializerOptions.MaxDepth = settings.MaxDepth;

                      options.JsonSerializerOptions.Converters.Clear();
                      options.JsonSerializerOptions.Converters.AddRange(settings.Converters);
                  }));
            return builder;
        }

        /// <summary>
        /// Add json.net serializer
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IMvcBuilder AddNewtonsoftSerializer(this IMvcBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder = builder.AddNewtonsoftJson();

            // Configure json serializer settings transiently to pick up all converters
            builder.Services.AddTransient<IConfigureOptions<MvcNewtonsoftJsonOptions>>(services =>
                new ConfigureNamedOptions<MvcNewtonsoftJsonOptions>(Options.DefaultName, options =>
                {
                    var provider = services.GetService<INewtonsoftSerializerSettingsProvider>();
                    var settings = provider?.Settings;
                    if (settings == null)
                    {
                        return;
                    }

                    options.SerializerSettings.Formatting = settings.Formatting;
                    options.SerializerSettings.NullValueHandling = settings.NullValueHandling;
                    options.SerializerSettings.DefaultValueHandling = settings.DefaultValueHandling;
                    options.SerializerSettings.ContractResolver = settings.ContractResolver;
                    options.SerializerSettings.DateFormatHandling = settings.DateFormatHandling;
                    options.SerializerSettings.MaxDepth = settings.MaxDepth;
                    options.SerializerSettings.Context = settings.Context;
                    options.SerializerSettings.Converters = settings.Converters;
                }));
            return builder;
        }
    }
}
