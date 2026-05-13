using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace DocuMind.Infrastructure.Embeddings;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindEmbeddings(this IServiceCollection services)
    {
        services.AddHttpClient<IEmbeddingClient, OpenAiEmbeddingClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;

            httpClient.BaseAddress = new Uri(options.Endpoint, UriKind.Absolute);
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
