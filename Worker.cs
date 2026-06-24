using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SubmissionProcessor.Worker;

public class Worker : BackgroundService
{
    IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;
    private IConnection _connection;
    private IChannel _channel;
    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            VirtualHost = "/",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
    }
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        string QueueName = _configuration["RabbitMQ:QueueName"] ?? "queue";
        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation("Connected to RabbitMQ and listening on '{QueueName}'...", QueueName);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string QueueName = _configuration["RabbitMQ:QueueName"] ?? "queue";

        stoppingToken.ThrowIfCancellationRequested();

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);
            try
            {
                _logger.LogInformation("Message received: {Message}", message);

                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing RabbitMQ message.");

                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };
        await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false, // Set to false to enforce explicit manual ACKs
                consumer: consumer,
                cancellationToken: stoppingToken
            );

        // Keep the background process alive while the service runs
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // 8. Safely dispose channels and connections upon application termination
        if (_channel is { IsOpen: true }) await _channel.CloseAsync(cancellationToken);
        if (_connection is { IsOpen: true }) await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
