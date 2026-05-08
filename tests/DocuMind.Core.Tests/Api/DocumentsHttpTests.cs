using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocuMind.Core.Documents;

namespace DocuMind.Core.Tests.Api;

public sealed class DocumentsHttpTests
{
    [Fact]
    public async Task PostDocuments_ShouldReturnCreatedWithLocationAndResponseShape()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("contract body"u8.ToArray()), "file", "manual.pdf");

        var response = await client.PostAsync("/v1/documents", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!.ToString();
        Assert.Contains("/v1/documents/", location);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("id", out var idProperty));
        Assert.True(Guid.TryParse(idProperty.GetString(), out var documentId));
        Assert.EndsWith($"/v1/documents/{documentId}", location, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Uploaded", root.GetProperty("status").GetString());
        Assert.Equal("manual.pdf", root.GetProperty("fileName").GetString());
        Assert.Equal("application/octet-stream", root.GetProperty("contentType").GetString());
        Assert.True(root.TryGetProperty("sizeInBytes", out _));
        Assert.True(root.TryGetProperty("uploadedAtUtc", out _));
    }

    [Fact]
    public async Task PostDocuments_ShouldReturnProblemDetailsWhenExtensionIsUnsupported()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("binary"u8.ToArray()), "file", "malware.exe");

        var response = await client.PostAsync("/v1/documents", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("Document upload failed.", root.GetProperty("title").GetString());
        Assert.Equal(415, root.GetProperty("status").GetInt32());
        Assert.Equal("unsupported_extension", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetDocumentById_ShouldReturnOkWithResponseShapeWhenDocumentExists()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var document = new Document(
            Guid.NewGuid(),
            new DocumentMetadata("manual.pdf", "application/pdf", 1234),
            "storage/uploads/manual.pdf",
            DateTimeOffset.Parse("2026-05-05T12:00:00Z"));
        factory.Repository.Seed(document);

        var response = await client.GetAsync($"/v1/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal(document.Id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("Uploaded", root.GetProperty("status").GetString());
        Assert.Equal("manual.pdf", root.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", root.GetProperty("contentType").GetString());
        Assert.Equal(1234, root.GetProperty("sizeInBytes").GetInt64());
        Assert.True(root.TryGetProperty("uploadedAtUtc", out _));
        Assert.True(root.TryGetProperty("updatedAtUtc", out _));
    }

    [Fact]
    public async Task GetDocumentById_ShouldReturnProblemDetailsWhenDocumentDoesNotExist()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/v1/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("Document retrieval failed.", root.GetProperty("title").GetString());
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("document_not_found", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("detail", out _));
    }
}
