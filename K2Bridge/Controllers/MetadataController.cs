﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace K2Bridge.Controllers
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using K2Bridge.HttpMessages;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.WebApiCompatShim;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handles the request that goes directly to the underlying elasticsearch
    /// instance that handles all metadata requests.
    /// </summary>
    [Route("")]
    [ApiController]
    public class MetadataController : ControllerBase
    {
        private readonly IHttpClientFactory clientFactory;
        private readonly ILogger<MetadataController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataController"/> class.
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="logger"></param>
        public MetadataController(IHttpClientFactory clientFactory, ILogger<MetadataController> logger)
        {
            this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handle metadata requests to the elasticsearch.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        [HttpPost]
        [HttpHead]
        [HttpPut]
        [HttpPatch]
        [HttpOptions]
        [HttpGet]
        public async Task<HttpResponseMessageResult> Passthrough()
        {
            try
            {
                return await this.PassthroughInternalAsync(this.HttpContext);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "An error occured when sending a passthrough request");
                throw;
            }
        }

        /// <summary>
        /// Take the original HTTP request and send it to the fallback elastic instance (passthrough).
        /// </summary>
        /// <param name="context">The original HTTP context.</param>
        /// <returns>The HTTP response delivered by the fallback elastic instance.</returns>
        internal async Task<HttpResponseMessageResult> PassthroughInternalAsync(HttpContext context)
        {
            var httpClient = this.clientFactory.CreateClient("elasticFallback");

            HttpRequestMessageFeature hreqmf = new HttpRequestMessageFeature(context);
            HttpRequestMessage remoteHttpRequestMessage = hreqmf.HttpRequestMessage;
            remoteHttpRequestMessage.Headers.Clear();

            // update the target host of the request
            remoteHttpRequestMessage.RequestUri =
                new Uri(httpClient.BaseAddress, remoteHttpRequestMessage.RequestUri.AbsolutePath);

            var remoteResponse = await httpClient.SendAsync(remoteHttpRequestMessage);
            context.Response.RegisterForDispose(remoteResponse);

            return new HttpResponseMessageResult(remoteResponse);
        }
    }
}