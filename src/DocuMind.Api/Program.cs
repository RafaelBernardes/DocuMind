using DocuMind.Api.Documents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDocuMindConfiguration(builder.Configuration);
builder.Services.AddDocuMindPersistence();
builder.Services.AddDocuMindStorage();
builder.Services.AddScoped<UploadDocumentHandler>();
builder.Services.AddSingleton<UploadDocumentRequestValidator>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "DocuMind.Api",
    Status = "Running"
}));

app.MapControllers();

app.Run();
