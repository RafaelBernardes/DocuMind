using System.Text;
using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Documents.TextExtraction;
using Microsoft.Extensions.DependencyInjection;

namespace DocuMind.Core.Tests.Documents;

public sealed class DocumentTextExtractorTests
{
    private readonly IDocumentTextExtractor _extractor = new ServiceCollection()
        .AddDocuMindTextExtraction()
        .BuildServiceProvider()
        .GetRequiredService<IDocumentTextExtractor>();

    [Fact]
    public async Task ExtractAsync_ShouldReturnNormalizedTextForTxt()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("line 1\r\nline 2"));

        var result = await _extractor.ExtractAsync("notes.txt", "text/plain", stream);

        Assert.True(result.IsSuccess);
        Assert.Equal("line 1\nline 2", result.Text);
        Assert.Null(result.FailureCode);
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNormalizedTextForMarkdown()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("# Title\rBody"));

        var result = await _extractor.ExtractAsync("notes.md", "text/markdown", stream);

        Assert.True(result.IsSuccess);
        Assert.Equal("# Title\nBody", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnTextForPdf()
    {
        await using var stream = new MemoryStream(CreateSimplePdfBytes("Hello PDF"));

        var result = await _extractor.ExtractAsync("notes.pdf", "application/pdf", stream);

        Assert.True(result.IsSuccess);
        Assert.Contains("Hello PDF", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnFailureForUnsupportedExtension()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("binary"));

        var result = await _extractor.ExtractAsync("notes.docx", "application/octet-stream", stream);

        Assert.False(result.IsSuccess);
        Assert.Equal(TextExtractionFailureCode.UnsupportedFileType, result.FailureCode);
        Assert.Contains(".docx", result.FailureReason);
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnFailureForInvalidPdf()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-a-real-pdf"));

        var result = await _extractor.ExtractAsync("broken.pdf", "application/pdf", stream);

        Assert.False(result.IsSuccess);
        Assert.Equal(TextExtractionFailureCode.InvalidContent, result.FailureCode);
    }

    private static byte[] CreateSimplePdfBytes(string text)
    {
        var escapedText = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n",
            CreateContentObject(escapedText),
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var offsets = new List<long> { 0 };
        foreach (var pdfObject in objects)
        {
            offsets.Add(stream.Position);
            writer.Write(pdfObject);
            writer.Flush();
        }

        var xrefPosition = stream.Position;
        writer.Write($"xref\n0 {offsets.Count}\n");
        writer.Write("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            writer.Write($"{offset:D10} 00000 n \n");
        }

        writer.Write("trailer\n");
        writer.Write($"<< /Root 1 0 R /Size {offsets.Count} >>\n");
        writer.Write("startxref\n");
        writer.Write($"{xrefPosition}\n");
        writer.Write("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string CreateContentObject(string escapedText)
    {
        var contentStream = $"BT\n/F1 24 Tf\n72 100 Td\n({escapedText}) Tj\nET";
        return $"4 0 obj\n<< /Length {contentStream.Length} >>\nstream\n{contentStream}\nendstream\nendobj\n";
    }
}
