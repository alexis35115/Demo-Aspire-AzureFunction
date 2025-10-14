using AspireApp.AppHost.Extensions;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<Aspire.Hosting.Azure.AzureServiceBusResource> serviceBus = builder
    .AddAzureServiceBus("myservicebus")
    .RunAsEmulator(c => c
        .WithLifetime(ContainerLifetime.Persistent));

serviceBus
    .AddServiceBusQueue("myqueue")
    .WithTestCommands();

builder.AddAzureFunctionsProject<Projects.AspireApp_FunctionApp>("functionapp")
    .WithReference(serviceBus)
    .WaitFor(serviceBus);

builder.Build().Run();
