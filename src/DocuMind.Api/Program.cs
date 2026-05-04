using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocuMindConfiguration(builder.Configuration);
builder.Services.AddDocuMindPersistence();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "DocuMind.Api",
    Status = "Running"
}));

app.Run();
