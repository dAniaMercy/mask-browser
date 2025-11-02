using Confluent.Kafka;
using System.Text.Json;

namespace MaskBrowser.Server.Services;

public class KafkaService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaService> _logger;
    private readonly string _bootstrapServers;

    public KafkaService(IConfiguration configuration, ILogger<KafkaService> logger)
    {
        _logger = logger;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers
        };
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "maskbrowser-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    public async Task PublishProfileEventAsync(string topic, object message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var kafkaMessage = new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = json
            };

            await _producer.ProduceAsync(topic, kafkaMessage);
            _logger.LogInformation("Published message to Kafka topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to Kafka topic: {Topic}", topic);
            throw;
        }
    }

    public async Task PublishContainerLogAsync(string containerId, string logMessage)
    {
        var logEvent = new
        {
            ContainerId = containerId,
            Message = logMessage,
            Timestamp = DateTime.UtcNow
        };

        await PublishProfileEventAsync("container-logs", logEvent);
    }

    public void SubscribeToTopic(string topic, Action<string> onMessage)
    {
        _consumer.Subscribe(topic);

        Task.Run(() =>
        {
            try
            {
                while (true)
                {
                    var result = _consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result != null)
                    {
                        onMessage(result.Message.Value);
                        _consumer.Commit(result);
                    }
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming from Kafka topic: {Topic}", topic);
            }
        });
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
        _consumer?.Close();
        _consumer?.Dispose();
    }
}

