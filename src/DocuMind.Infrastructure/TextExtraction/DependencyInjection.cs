using DocuMind.Core.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Infrastructure.TextExtraction;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuMindTextExtraction(this IServiceCollection services)
    {
        services.AddSingleton<ITextExtractionStrategy, TxtTextExtractionStrategy>();
        services.AddSingleton<ITextExtractionStrategy, MarkdownTextExtractionStrategy>();
        services.AddSingleton<ITextExtractionStrategy, PdfTextExtractionStrategy>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();

        return services;
    }
}
