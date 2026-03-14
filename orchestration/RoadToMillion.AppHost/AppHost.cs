var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()  // Persists database data across container restarts
    .WithPgAdmin()
    .AddDatabase("roadtomilliondb");

var api = builder.AddProject<Projects.RoadToMillion_Api>("api")
    .WithEndpoint("https", e => e.Port = 7100)
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddProject<Projects.RoadToMillion_Web>("web")
    .WithEndpoint("https", e => e.Port = 7200)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
