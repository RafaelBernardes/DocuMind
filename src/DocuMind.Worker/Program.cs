using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Documents.Chunking;
using DocuMind.Infrastructure.Documents.TextExtraction;
using DocuMind.Infrastructure.Embeddings;
using DocuMind.Infrastructure.Messaging;
using DocuMind.Infrastructure.Messaging.RabbitMq;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Storage;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDocuMindConfiguration(builder.Configuration);
builder.Services.AddDocuMindPersistence();
builder.Services.AddDocuMindStorage();
builder.Services.AddDocuMindChunking();
builder.Services.AddDocuMindEmbeddings();
builder.Services.AddDocuMindTextExtraction();
builder.Services.AddDocuMindMessaging();
builder.Services.AddHostedService<DocumentIngestionConsumerHostedService>();

var host = builder.Build();
await host.RunAsync();
