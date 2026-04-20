using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AspireApp.ServiceDefaults;

// Ajoute les services .NET Aspire communs : découverte de services, résilience, vérifications de santé et OpenTelemetry.
// Ce projet doit être référencé par chaque projet de service de votre solution.
// Pour en savoir plus sur l'utilisation de ce projet, consultez https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Activer la résilience par défaut
            http.AddStandardResilienceHandler();

            // Activer la découverte de services par défaut
            http.AddServiceDiscovery();
        });

        // Décommenter ce qui suit pour restreindre les schémas autorisés pour la découverte de services.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    // Décommenter la ligne suivante pour activer l'instrumentation gRPC (nécessite le package OpenTelemetry.Instrumentation.GrpcNetClient)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Décommenter les lignes suivantes pour activer l'exportateur Azure Monitor (nécessite le package Azure.Monitor.OpenTelemetry.AspNetCore)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Ajouter une vérification de vivacité par défaut pour s'assurer que l'application répond
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // L'ajout de points de terminaison de vérification de santé dans des environnements hors développement a des implications en matière de sécurité.
        // Consultez https://aka.ms/dotnet/aspire/healthchecks pour plus de détails avant d'activer ces points de terminaison hors développement.
        if (app.Environment.IsDevelopment())
        {
            // Toutes les vérifications de santé doivent réussir pour que l'application soit prête à accepter du trafic après le démarrage
            app.MapHealthChecks("/health");

            // Seules les vérifications de santé marquées avec le tag "live" doivent réussir pour que l'application soit considérée comme vivante
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
