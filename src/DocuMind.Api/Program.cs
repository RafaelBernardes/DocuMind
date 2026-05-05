using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocuMindConfiguration(builder.Configuration);
builder.Services.AddDocuMindPersistence();
builder.Services.AddDocuMindStorage();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "DocuMind.Api",
    Status = "Running"
}));

app.Run();
