using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace MaskBrowser.Server.Infrastructure;

public class RabbitMQService : IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQService> _logger;
    private readonly string _exchangeName = "maskbrowser";

    public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "maskqueue",
            Password = configuration["RabbitMQ:Password"] ?? "MaskQueue123"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange and queues
        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);

        _channel.QueueDeclare("container-events", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare("scaling-events", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare("profile-sync", durable: true, exclusive: false, autoDelete: false);

        _channel.QueueBind("container-events", _exchangeName, "container.*");
        _channel.QueueBind("scaling-events", _exchangeName, "scaling.*");
        _channel.QueueBind("profile-sync", _exchangeName, "profile.*");
    }

    public void Publish<T>(string routingKey, T message)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: routingKey,
            basicProperties: null,
            body: body
        );
        _logger.LogInformation("Published message to {RoutingKey}", routingKey);
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

