using DocuMind.Api.Documents.Commands.UploadDocument;
using DocuMind.Api.Documents.Queries.GetDocumentById;
using DocuMind.Infrastructure.Chunking;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Storage;
using DocuMind.Infrastructure.TextExtraction;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDocuMindConfiguration(builder.Configuration);
builder.Services.AddDocuMindPersistence();
builder.Services.AddDocuMindStorage();
builder.Services.AddDocuMindChunking();
builder.Services.AddDocuMindTextExtraction();
builder.Services.AddScoped<GetDocumentByIdQueryHandler>();
builder.Services.AddScoped<UploadDocumentCommandHandler>();
builder.Services.AddSingleton<UploadDocumentCommandValidator>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "DocuMind.Api",
    Status = "Running"
}));

app.MapControllers();

app.Run();

public partial class Program;
