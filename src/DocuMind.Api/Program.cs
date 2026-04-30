using DocuMind.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocuMindConfiguration(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "DocuMind.Api",
    Status = "Running"
}));

app.Run();
