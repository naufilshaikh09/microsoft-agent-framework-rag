var builder = DistributedApplication.CreateBuilder(args);

// Qdrant is opt-in: only provision when Ai:VectorStore:Provider = "Qdrant" in config.
// When using InMemory (default), the qdrant reference is simply ignored by the API.
var qdrant = builder.AddQdrant("qdrant");

var api = builder.AddProject<Projects.AgentFrameworkRag_Api>("agentframeworkrag-api")
    .WithReference(qdrant)
    .WithExternalHttpEndpoints();

builder.AddNpmApp("agentframeworkrag-frontend", "../react-frontend", "dev")
    .WithHttpEndpoint(port: 5173, env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("http"));

builder.Build().Run();
