using Aspire.Hosting.Azure;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text;

namespace AspireApp.AppHost.Extensions;

public static class ServiceBusExtensions
{
    public static IResourceBuilder<AzureServiceBusQueueResource> WithTestCommands(
        this IResourceBuilder<AzureServiceBusQueueResource> builder)
    {
        builder.ApplicationBuilder.Services.AddSingleton(provider =>
        {
            var connectionString = builder.Resource.Parent.ConnectionStringExpression
                .GetValueAsync(CancellationToken.None).GetAwaiter().GetResult();
            return new ServiceBusClient(connectionString);
        });

        builder.WithCommand("SendSbMessage", "Send Service Bus message", executeCommand: async (c) =>
        {
            ServiceBusClient sbClient = c.ServiceProvider.GetRequiredService<ServiceBusClient>();

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

            await sbClient.CreateSender(builder.Resource.QueueName)
                .SendMessageAsync(message);

            return new ExecuteCommandResult { Success = true };
        });

        return builder;
    }
}
