var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.RoadToMillion_Api>("api")
    .WithEndpoint("https", e => e.Port = 7100);

builder.AddProject<Projects.RoadToMillion_Web>("web")
    .WithEndpoint("https", e => e.Port = 7200)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
