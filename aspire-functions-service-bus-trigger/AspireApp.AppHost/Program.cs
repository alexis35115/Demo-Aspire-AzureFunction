using AspireApp.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder
    .AddAzureServiceBus("myservicebus")
    .RunAsEmulator(c => c
        .WithLifetime(ContainerLifetime.Persistent));

serviceBus
    .AddServiceBusQueue("myqueue")
    .WithTestCommands();

// Add SQL Server container and Communication database
var saPassword = builder.AddParameter("sql-sa-password", secret: true);
var sql = builder.AddSqlServer("sql", saPassword);
var db = sql.AddDatabase("Communication");

// Add DB migrator as a project that runs at startup
var migrator = builder.AddProject<Projects.AspireApp_DbMigrator>("dbmigrator")
    .WithReference(db)
    .WaitFor(db);

builder.AddAzureFunctionsProject<Projects.AspireApp_FunctionApp>("functionapp")
    .WithReference(serviceBus)
    .WithReference(db)
    .WaitFor(migrator) // ensure DB schema exists before starting the Function
    .WaitFor(serviceBus)
    .WaitFor(db);

builder.Build().Run();
