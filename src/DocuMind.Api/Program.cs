using DocuMind.Api.Documents.Commands.UploadDocument;
using DocuMind.Api.Documents.Queries.GetDocumentById;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Messaging;
using DocuMind.Infrastructure.Messaging.Outbox;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDocuMindConfiguration(builder.Configuration, includeOpenAi: false);
builder.Services.AddDocuMindPersistence();
builder.Services.AddDocuMindStorage();
builder.Services.AddDocuMindMessaging();
builder.Services.AddScoped<GetDocumentByIdQueryHandler>();
builder.Services.AddScoped<UploadDocumentCommandHandler>();
builder.Services.AddSingleton<UploadDocumentCommandValidator>();
builder.Services.AddHostedService<OutboxPublisherHostedService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "DocuMind.Api",
    Status = "Running"
}));

app.MapControllers();

app.Run();

public partial class Program;
