IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddDotnetProject(
    "asi-backbone-api",
    "../PlainAspNetCoreHost/AsiBackbone.Samples.PlainAspNetCoreHost.csproj")
    .WithEnvironment("ConnectionStrings__AsiBackbone", "Data Source=asi-backbone-aspire-sample.db")
    .WithEnvironment("ASIBACKBONE_SAMPLE_HOST", "AspireAppHost")
    .WithEnvironment("ASIBACKBONE_SAMPLE_DOCS", "https://asibackbone.github.io/AsiBackbone/articles/aspire-apphost-sample.html");

builder.Build().Run();
