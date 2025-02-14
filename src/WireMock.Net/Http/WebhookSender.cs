using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Stef.Validation;
using WireMock.Models;
using WireMock.Settings;
using WireMock.Transformers;
using WireMock.Transformers.Handlebars;
using WireMock.Transformers.Scriban;
using WireMock.Types;
using WireMock.Util;

namespace WireMock.Http;

internal class WebhookSender
{
    private const string ClientIp = "::1";

    private readonly WireMockServerSettings _settings;

    public WebhookSender(WireMockServerSettings settings)
    {
        _settings = Guard.NotNull(settings);
    }

    public Task<HttpResponseMessage> SendAsync(HttpClient client, IMapping mapping, IWebhookRequest request, IRequestMessage originalRequestMessage, IResponseMessage originalResponseMessage)
    {
        Guard.NotNull(client);
        Guard.NotNull(mapping);
        Guard.NotNull(request);
        Guard.NotNull(originalRequestMessage);
        Guard.NotNull(originalResponseMessage);

        IBodyData? bodyData;
        IDictionary<string, WireMockList<string>>? headers;
        if (request.UseTransformer == true)
        {
            ITransformer responseMessageTransformer;
            switch (request.TransformerType)
            {
                case TransformerType.Handlebars:
                    var factoryHandlebars = new HandlebarsContextFactory(_settings.FileSystemHandler, _settings.HandlebarsRegistrationCallback);
                    responseMessageTransformer = new Transformer(factoryHandlebars);
                    break;

                case TransformerType.Scriban:
                case TransformerType.ScribanDotLiquid:
                    var factoryDotLiquid = new ScribanContextFactory(_settings.FileSystemHandler, request.TransformerType);
                    responseMessageTransformer = new Transformer(factoryDotLiquid);
                    break;

                default:
                    throw new NotImplementedException($"TransformerType '{request.TransformerType}' is not supported.");
            }

            (bodyData, headers) = responseMessageTransformer.Transform(mapping, originalRequestMessage, originalResponseMessage, request.BodyData, request.Headers, request.TransformerReplaceNodeOptions);
        }
        else
        {
            bodyData = request.BodyData;
            headers = request.Headers;
        }

        // Create RequestMessage
        var requestMessage = new RequestMessage(
            new UrlDetails(request.Url),
            request.Method,
            ClientIp,
            bodyData,
            headers?.ToDictionary(x => x.Key, x => x.Value.ToArray())
        )
        {
            DateTime = DateTime.UtcNow
        };

        // Create HttpRequestMessage
        var httpRequestMessage = HttpRequestMessageHelper.Create(requestMessage, request.Url);

        // Call the URL
        return client.SendAsync(httpRequestMessage);
    }
}