using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AspireApp.FunctionApp;

public class ServiceBusFunction
{
    private readonly ILogger<ServiceBusFunction> _logger;
    private readonly IConfiguration _configuration;

    public ServiceBusFunction(ILogger<ServiceBusFunction> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public record Communication(Guid Id, string Type, string Destination, string Subject, string Body, DateTimeOffset RequestedAtUtc);

    [Function(nameof(ServiceBusFunction))]
    public async Task Run(
        [ServiceBusTrigger("myqueue", Connection = "myservicebus")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        try
        {
            if (message.Body == null)
            {
                _logger.LogWarning("Received message with empty body");
                await messageActions.AbandonMessageAsync(message);
                return;
            }

            var comm = JsonSerializer.Deserialize<Communication>(message.Body);
            if (comm is null)
            {
                _logger.LogWarning("Failed to deserialize message body to Communication");
                var props = new Dictionary<string, object>
                {
                    ["DeadLetterReason"] = "DeserializationFailed",
                    ["DeadLetterErrorDescription"] = "Body could not be parsed as Communication"
                };
                await messageActions.DeadLetterMessageAsync(message, props);
                return;
            }

            // Get connection string injected by Aspire for the Communication database
            var connectionString = _configuration.GetConnectionString("Communication");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError("Connection string 'Communication' not found");
                await messageActions.AbandonMessageAsync(message);
                return;
            }

            await InsertCommunicationAsync(connectionString, comm);

            _logger.LogInformation("Inserted Communication {Id} of type {Type}", comm.Id, comm.Type);
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Service Bus message");
            await messageActions.AbandonMessageAsync(message);
        }
    }

    private static async Task InsertCommunicationAsync(string connectionString, Communication comm)
    {
        const string insertSql = @"INSERT INTO [dbo].[Communication] ([Id], [Type], [Destination], [Subject], [Body], [RequestedAtUtc])
VALUES (@Id, @Type, @Destination, @Subject, @Body, @RequestedAtUtc)";
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(insertSql, conn);
        cmd.Parameters.AddWithValue("@Id", comm.Id);
        cmd.Parameters.AddWithValue("@Type", comm.Type);
        cmd.Parameters.AddWithValue("@Destination", comm.Destination);
        cmd.Parameters.AddWithValue("@Subject", (object?)comm.Subject ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Body", (object?)comm.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RequestedAtUtc", comm.RequestedAtUtc);
        await cmd.ExecuteNonQueryAsync();
    }
}
