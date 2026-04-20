using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AspireApp.FunctionApp;

public partial class ServiceBusFunction(ILogger<ServiceBusFunction> logger,
    IConfiguration configuration)
{
    private readonly ILogger<ServiceBusFunction> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public sealed record Communication(
        Guid Id,
        string Type,
        string Destination,
        string Subject,
        string Body,
        DateTimeOffset RequestedAtUtc);

    [Function(nameof(ServiceBusFunction))]
    public async Task Run(
        [ServiceBusTrigger("myqueue", Connection = "myservicebus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        try
        {
            if (message.Body is null)
            {
                ServiceBusFunctionLog.ReceivedMessageWithEmptyBody(_logger);
                await messageActions.AbandonMessageAsync(message);
                return;
            }

            var communication = JsonSerializer.Deserialize<Communication>(message.Body);
            if (communication is null)
            {
                ServiceBusFunctionLog.FailedToDeserialize(_logger);
                var props = new Dictionary<string, object>
                {
                    ["DeadLetterReason"] = "DeserializationFailed",
                    ["DeadLetterErrorDescription"] = "Le corps du message n'a pas pu être désérialisé en Communication"
                };
                await messageActions.DeadLetterMessageAsync(message, props);
                return;
            }

            // Récupérer la chaîne de connexion injectée par Aspire pour la base de données Communication
            var connectionString = _configuration.GetConnectionString("Communication");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                ServiceBusFunctionLog.MissingConnectionString(_logger);
                await messageActions.AbandonMessageAsync(message);
                return;
            }

            await InsertCommunicationAsync(connectionString, communication);

            ServiceBusFunctionLog.InsertedCommunication(_logger, communication.Id, communication.Type);
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            ServiceBusFunctionLog.ErrorHandling(_logger, ex);
            await messageActions.AbandonMessageAsync(message);
        }
    }

    private static async Task InsertCommunicationAsync(
        string connectionString,
        Communication communication)
    {
        const string insertSql = @"INSERT INTO [dbo].[Communication] ([Id], [Type], [Destination], [Subject], [Body], [RequestedAtUtc])
VALUES (@Id, @Type, @Destination, @Subject, @Body, @RequestedAtUtc)";
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(insertSql, conn);
        cmd.Parameters.AddWithValue("@Id", communication.Id);
        cmd.Parameters.AddWithValue("@Type", communication.Type);
        cmd.Parameters.AddWithValue("@Destination", communication.Destination);
        cmd.Parameters.AddWithValue("@Subject", (object?)communication.Subject ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Body", (object?)communication.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RequestedAtUtc", communication.RequestedAtUtc);
        await cmd.ExecuteNonQueryAsync();
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Message reçu avec un corps vide")]
    public static partial void ReceivedMessageWithEmptyBody(ILogger logger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Échec de la désérialisation du corps du message en Communication")]
    public static partial void FailedToDeserialize(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Chaîne de connexion 'Communication' introuvable")]
    public static partial void MissingConnectionString(ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Communication {Id} de type {Type} insérée")]
    public static partial void InsertedCommunication(ILogger logger, Guid id, string type);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Erreur lors du traitement du message Service Bus")]
    public static partial void ErrorHandling(ILogger logger, Exception exception);
}

internal static partial class ServiceBusFunctionLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Message reçu avec un corps vide")]
    public static partial void ReceivedMessageWithEmptyBody(ILogger logger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Échec de la désérialisation du corps du message en Communication")]
    public static partial void FailedToDeserialize(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Chaîne de connexion 'Communication' introuvable")]
    public static partial void MissingConnectionString(ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Communication {Id} de type {Type} insérée")]
    public static partial void InsertedCommunication(ILogger logger, Guid id, string type);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Erreur lors du traitement du message Service Bus")]
    public static partial void ErrorHandling(ILogger logger, Exception exception);
}