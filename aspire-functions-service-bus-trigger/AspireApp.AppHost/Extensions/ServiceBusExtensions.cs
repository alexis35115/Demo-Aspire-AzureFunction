using Aspire.Hosting.Azure;
using Azure.Messaging.ServiceBus;
using System.Text;
using System.Text.Json;

namespace AspireApp.AppHost.Extensions;

public static class ServiceBusExtensions
{
    public static IResourceBuilder<AzureServiceBusQueueResource> WithTestCommands(
        this IResourceBuilder<AzureServiceBusQueueResource> builder)
    {
        builder.WithCommand("SendSbMessage", "Send Service Bus message", executeCommand: async (c) =>
        {
            // Récupère la connexion une fois que la ressource est prête
            var connectionString = await builder.Resource.Parent.ConnectionStringExpression
                .GetValueAsync(c.CancellationToken);

            await using var sbClient = new ServiceBusClient(connectionString);
            await using var sender = sbClient.CreateSender(builder.Resource.QueueName);

            var payload = new
            {
                Id = Guid.NewGuid(),
                Type = "Email",
                Destination = "user@example.com",
                Subject = "Welcome",
                Body = "Hello from Aspire!",
                RequestedAtUtc = DateTimeOffset.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json))
            {
                ContentType = "application/json",
                CorrelationId = payload.Id.ToString()
            };

            await sender.SendMessageAsync(message, c.CancellationToken);

            return new ExecuteCommandResult { Success = true };
        });

        return builder;
    }
}
