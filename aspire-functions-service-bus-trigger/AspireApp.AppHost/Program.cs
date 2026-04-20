using AspireApp.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder
    .AddAzureServiceBus("myservicebus")
    .RunAsEmulator(c => c
        .WithLifetime(ContainerLifetime.Persistent));

serviceBus
    .AddServiceBusQueue("myqueue")
    .WithTestCommands();

// Ajouter le conteneur SQL Server et la base de données Communication
var saPassword = builder.AddParameter("sql-sa-password", secret: true);
var sql = builder.AddSqlServer("sql", saPassword);
var db = sql.AddDatabase("Communication");

// Ajouter le migrateur de base de données en tant que projet qui s'exécute au démarrage
var migrator = builder.AddProject<Projects.AspireApp_DbMigrator>("dbmigrator")
    .WithReference(db)
    .WaitFor(db);

builder.AddAzureFunctionsProject<Projects.AspireApp_FunctionApp>("functionapp")
    .WithReference(serviceBus)
    .WithReference(db)
    .WaitFor(migrator) // s'assurer que le schéma de la base de données existe avant de démarrer la Function
    .WaitFor(serviceBus)
    .WaitFor(db);

builder.Build().Run();
