using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SubmissionProcessor.Worker.Data;
using SubmissionProcessor.Worker.DTO;
using SubmissionProcessor.Worker.Enums;
using SubmissionProcessor.Worker.Helpers;
using SubmissionProcessor.Worker.Models;
using SubmissionProcessor.Worker.Services;

namespace SubmissionProcessor.Worker;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICallerService _callerService;
    private IConnection? _connection;
    private IChannel? _channel;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IServiceProvider serviceProvider, ICallerService callerService)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _callerService = callerService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            VirtualHost = "/",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        string queueName = _configuration["RabbitMQ:QueueName"] ?? "queue";
        string dlxExchange = "dlx.exchange";
        string dlxQueue = queueName + ".dead";
        string dlxRoutingKey = queueName + ".dead";

        await _channel.ExchangeDeclareAsync(dlxExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(dlxQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(dlxQueue, dlxExchange, dlxRoutingKey, cancellationToken: cancellationToken);

        var mainQueueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", dlxExchange },
            { "x-dead-letter-routing-key", dlxRoutingKey }
        };

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken
        );

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

        _logger.LogInformation("Connected to RabbitMQ and listening on '{QueueName}'...", queueName);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string queueName = _configuration["RabbitMQ:QueueName"] ?? "queue";
        stoppingToken.ThrowIfCancellationRequested();

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);
            _logger.LogInformation("Message consumed");
            using (var scope = _serviceProvider.CreateScope())
            {
                AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                IFileStorageService fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                try
                {
                    SubmissionProcessingMessage submissionProcessingMessage = JsonSerializer.Deserialize<SubmissionProcessingMessage>(message)!;

                    ProcessingJob? processingJob = context.ProcessingJob
                        .FirstOrDefault(job => job.CorrelationId == submissionProcessingMessage.CorrelationId);

                    if (processingJob == null)
                    {
                        _logger.LogWarning("Job not found for CorrelationId: {CorrelationId}. Dropping message.", submissionProcessingMessage.CorrelationId);

                        await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                        return;
                    }

                    processingJob.StartedTime = DateHelper.Now();

                    if (processingJob.Attempts >= 3)
                    {
                        _logger.LogError("Max attempts reached for Job ID: {JobId}. Moving to DLX.", processingJob.Id);
                        processingJob.Status = GlobalEnums.ProcessingJobStatus.Failed;
                        _logger.LogInformation("Processing Job status changed to Failed, ProcessingJobId: {}", processingJob.Id);
                        processingJob.CompletedTime = DateHelper.Now();
                        await context.SaveChangesAsync();

                        await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    processingJob.Attempts += 1;
                    processingJob.Status = GlobalEnums.ProcessingJobStatus.Processing;
                    _logger.LogInformation("Processing Job status changed to Processing, ProcessingJobId: {}", processingJob.Id);
                    await context.SaveChangesAsync();

                    SubmissionFileMetaData? fileMetaData = context.SubmissionFileMetaData
                        .FirstOrDefault(file => file.Id == submissionProcessingMessage.FileId);

                    if (fileMetaData == null)
                    {
                        processingJob.Status = GlobalEnums.ProcessingJobStatus.Failed;
                        _logger.LogInformation("Processing Job status changed to Failed, ProcessingJobId: {}", processingJob.Id);
                        processingJob.CompletedTime = DateHelper.Now();
                        await context.SaveChangesAsync();

                        await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    using (FileStream fileStream = await fileStorageService.OpenReadAsync(fileMetaData.GeneratedStorageName))
                    {
                        string newCheckSum = CheckSumHelper.GetFileChecksum(fileStream);

                        if (newCheckSum != fileMetaData.Checksum)
                        {
                            _logger.LogError("Checksum mismatch for File ID: {FileId}", fileMetaData.Id);
                            processingJob.Status = GlobalEnums.ProcessingJobStatus.Failed;
                            _logger.LogInformation("Processing Job status changed to Failed, ProcessingJobId: {}", processingJob.Id);
                            processingJob.CompletedTime = DateHelper.Now();
                            await context.SaveChangesAsync();

                            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                            return;
                        }
                    }

                    _logger.LogInformation("Message successfully processed: {Message}", message);

                    processingJob.CompletedTime = DateHelper.Now();
                    processingJob.Status = GlobalEnums.ProcessingJobStatus.Completed;
                    _logger.LogInformation("Processing Job status changed to Completed, ProcessingJobId: {}", processingJob.Id);
                    await context.SaveChangesAsync();

                    await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    using HttpResponseMessage response = await _callerService.GetById(processingJob.CorrelationId);

                    string content = await response.Content.ReadAsStringAsync(stoppingToken);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Success! Status Code: {StatusCode}. Response data: {Data}",
                            response.StatusCode, content);
                    }
                    else
                    {
                        _logger.LogWarning("Request failed with Status Code: {StatusCode}. Reason/Error: {Error}",
                            response.StatusCode, content);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected crash processing message. Requeuing to main queue.");
                    await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            }
        };

        await _channel!.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }
}
