using System.ClientModel;
using Azure.AI.OpenAI;
using CommunityToolkit.VectorData.InMemory;
using CommunityToolkit.VectorData.Qdrant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI;
using Qdrant.Client;
using Scalar.AspNetCore;
using AgentFrameworkRag.Api.Agents;
using AgentFrameworkRag.Api.Configuration;
using AgentFrameworkRag.Api.Endpoints;
using AgentFrameworkRag.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var aiOptions = builder.Configuration
    .GetSection(AiOptions.SectionName)
    .Get<AiOptions>() ?? new AiOptions();

builder.Services.Configure<RagOptions>(
    builder.Configuration.GetSection(RagOptions.SectionName));

const int EmbeddingDimensions = 1536;

if (aiOptions.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    var azureClient = new AzureOpenAIClient(
        new Uri(aiOptions.AzureOpenAI.Endpoint),
        new ApiKeyCredential(aiOptions.AzureOpenAI.ApiKey));

    builder.Services.AddSingleton<IChatClient>(sp =>
        azureClient.GetChatClient(aiOptions.AzureOpenAI.ChatDeployment).AsIChatClient());

    builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        azureClient.GetEmbeddingClient(aiOptions.AzureOpenAI.EmbeddingDeployment)
            .AsIEmbeddingGenerator(EmbeddingDimensions));
}
else
{
    var openAiClient = new OpenAIClient(new ApiKeyCredential(aiOptions.OpenAI.ApiKey));

    builder.Services.AddSingleton<IChatClient>(sp =>
        openAiClient.GetChatClient(aiOptions.OpenAI.ChatModel).AsIChatClient());

    builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        openAiClient.GetEmbeddingClient(aiOptions.OpenAI.EmbeddingModel)
            .AsIEmbeddingGenerator(EmbeddingDimensions));
}

if (aiOptions.VectorStore.Provider.Equals("Qdrant", StringComparison.OrdinalIgnoreCase))
{
    builder.AddQdrantClient("qdrant");
    builder.Services.AddSingleton<VectorStore>(sp =>
        new QdrantVectorStore(sp.GetRequiredService<QdrantClient>(), ownsClient: false));
    builder.Services.AddHostedService<DocumentRegistrySeeder>();
}
else
{
    builder.Services.AddSingleton<VectorStore, InMemoryVectorStore>();
}

builder.Services.AddSingleton<DocumentRegistry>();
builder.Services.AddSingleton<ITextExtractor, PlainTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<ChatRequestContext>();
builder.Services.AddScoped<SourceCollector>();
builder.Services.AddScoped<DocumentSearchAdapter>();
builder.Services.AddScoped<QueryContextualizer>();
builder.Services.AddScoped<IDocumentRetrievalService, DocumentRetrievalService>();
builder.Services.AddScoped<DocumentRetrievalService>();
builder.Services.AddScoped<DocumentIndexerService>();
builder.Services.AddScoped<RagAgentFactory>();
builder.Services.AddScoped<IRagService, RagService>();

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddOpenApi();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapChatEndpoints();
app.MapDocumentEndpoints();
app.MapDefaultEndpoints();

app.Run();
