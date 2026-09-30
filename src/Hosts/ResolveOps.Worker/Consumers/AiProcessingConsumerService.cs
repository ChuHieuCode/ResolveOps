using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ResolveOps.Domain.Ai;
using ResolveOps.Messaging.Options;
using ResolveOps.Modules.Ai.Contracts;
using ResolveOps.Modules.Ai.Services.Completion;
using ResolveOps.Modules.Ai.Services.Governance;
using ResolveOps.Modules.Ai.Services.Rag;
using ResolveOps.Modules.Ai.Services.Security;
using ResolveOps.Persistence;

namespace ResolveOps.Worker.Consumers;

public sealed record AiTaskQueueMessage(
    Guid TaskId,
    Guid TenantId,
    string TaskType,
    string SourceEntityType,
    Guid SourceEntityId,
    string InputText,
    Guid? CarrierId
);

public sealed class AiProcessingConsumerService : BackgroundService
{
    private const string _queueName = "resolveops.ai-processing";
    private const string _consumerName = "AiProcessingConsumer";
    private const string _dlxExchangeName = "resolveops.dlx";
    private const string _deadLetterQueueName = "resolveops.dead-letter";
    private const string _deadLetterRoutingKey = "ai.failed";

    private static readonly Action<ILogger, string, Exception?> _logChannelCreationError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "ChannelCreationError"),
            "Failed to create RabbitMQ channel for AI processing queue {QueueName}");

    private static readonly Action<ILogger, string, Exception?> _logConsumerStarted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "ConsumerStarted"),
            "AiProcessingConsumerService started listening on queue {QueueName}");

    private static readonly Action<ILogger, string, Exception?> _logProcessError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "ProcessError"),
            "Error processing AI task from queue {QueueName}, routing to DLQ.");

    private static readonly Action<ILogger, string, Exception?> _logMalformedPayload =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "MalformedPayload"),
            "Malformed AI task queue payload received: {Payload}");

    private static readonly Action<ILogger, Guid, Guid, Exception?> _logSkipped =
        LoggerMessage.Define<Guid, Guid>(LogLevel.Information, new EventId(5, "SkippedKillSwitch"),
            "AI processing is disabled by kill switch for tenant {TenantId}. Skipping task {TaskId}.");

    private static readonly Action<ILogger, Guid, AiTaskType, decimal, AiReviewStatus, Exception?> _logCompleted =
        LoggerMessage.Define<Guid, AiTaskType, decimal, AiReviewStatus>(LogLevel.Information, new EventId(6, "TaskCompleted"),
            "Completed AI task {TaskId} ({TaskType}) with confidence {Confidence} and review status {ReviewStatus}.");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConnection _connection;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<RabbitMqConsumerOptions> _options;
    private readonly ILogger<AiProcessingConsumerService> _logger;

    public AiProcessingConsumerService(
        IConnection connection,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<RabbitMqConsumerOptions> options,
        ILogger<AiProcessingConsumerService> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IChannel channel;
        try
        {
            channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            _logChannelCreationError(_logger, _queueName, ex);
            return;
        }

        await using (channel)
        {
            // 1. Declare DLX
            await channel.ExchangeDeclareAsync(
                exchange: _dlxExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            // 2. Declare DLQ
            await channel.QueueDeclareAsync(
                queue: _deadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            await channel.QueueBindAsync(
                queue: _deadLetterQueueName,
                exchange: _dlxExchangeName,
                routingKey: _deadLetterRoutingKey,
                cancellationToken: stoppingToken);

            // 3. Declare main queue with DLQ routing
            var queueArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _dlxExchangeName,
                ["x-dead-letter-routing-key"] = _deadLetterRoutingKey
            };

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: stoppingToken);

            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: (ushort)_options.Value.PrefetchCount,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    await ProcessMessageAsync(ea.Body.ToArray(), stoppingToken);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logProcessError(_logger, _queueName, ex);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            };

            await channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumerTag: _consumerName,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logConsumerStarted(_logger, _queueName, null);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
            }
        }
    }

    private async Task ProcessMessageAsync(byte[] body, CancellationToken cancellationToken)
    {
        var json = Encoding.UTF8.GetString(body);
        var msg = JsonSerializer.Deserialize<AiTaskQueueMessage>(json, _jsonOptions);
        if (msg is null || msg.TaskId == Guid.Empty)
        {
            _logMalformedPayload(_logger, json, null);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aiCompletionService = scope.ServiceProvider.GetRequiredService<IAiCompletionService>();
        var ragService = scope.ServiceProvider.GetRequiredService<IAiKnowledgeRetrievalService>();
        var featureFlagService = scope.ServiceProvider.GetRequiredService<IAiFeatureFlagService>();

        if (!featureFlagService.IsAiEnabled(msg.TenantId))
        {
            _logSkipped(_logger, msg.TenantId, msg.TaskId, null);
            return;
        }

        var task = await dbContext.AiTasks
            .FirstOrDefaultAsync(t => t.Id == msg.TaskId, cancellationToken);

        if (task is null || task.Status != AiTaskStatus.Pending)
        {
            return;
        }

        task.MarkProcessing();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Retrieve RAG knowledge if carrier or policy context is applicable
        string? knowledgeContext = null;
        if (msg.CarrierId.HasValue || task.TaskType == AiTaskType.DraftCommunication || task.TaskType == AiTaskType.EvidenceRecommendation)
        {
            var ragResults = await ragService.SearchSimilarAsync(
                msg.TenantId,
                msg.InputText,
                limit: 3,
                carrierId: msg.CarrierId,
                cancellationToken: cancellationToken);

            if (ragResults.Count > 0)
            {
                knowledgeContext = string.Join("\n---\n", ragResults.Select(r => $"[{r.KnowledgeType}] {r.Title}:\n{r.ContentChunk}"));
            }
        }

        var promptRequest = new AiPromptRequest(
            TenantId: msg.TenantId,
            TaskType: task.TaskType,
            SystemPrompt: PromptInjectionShield.SystemSecurityDirective,
            UntrustedInput: PromptInjectionShield.WrapUntrustedData(msg.InputText),
            KnowledgeContext: knowledgeContext);

        string rawJson;
        decimal confidence;
        string modelProvider;
        string modelName;
        string promptVersion;

        switch (task.TaskType)
        {
            case AiTaskType.EmailClassification:
            case AiTaskType.NoteClassification:
                var classResult = await aiCompletionService.CompleteStructuredAsync<EmailClassificationOutput>(promptRequest, cancellationToken);
                rawJson = classResult.RawJson;
                confidence = classResult.Confidence;
                modelProvider = classResult.ModelProvider;
                modelName = classResult.ModelName;
                promptVersion = classResult.PromptVersion;
                break;

            case AiTaskType.TimelineSummary:
                var timeResult = await aiCompletionService.CompleteStructuredAsync<TimelineSummaryOutput>(promptRequest, cancellationToken);
                rawJson = timeResult.RawJson;
                confidence = timeResult.Confidence;
                modelProvider = timeResult.ModelProvider;
                modelName = timeResult.ModelName;
                promptVersion = timeResult.PromptVersion;
                break;

            case AiTaskType.EvidenceRecommendation:
                var evidResult = await aiCompletionService.CompleteStructuredAsync<EvidenceRecommendationOutput>(promptRequest, cancellationToken);
                rawJson = evidResult.RawJson;
                confidence = evidResult.Confidence;
                modelProvider = evidResult.ModelProvider;
                modelName = evidResult.ModelName;
                promptVersion = evidResult.PromptVersion;
                break;

            case AiTaskType.DraftCommunication:
                var draftResult = await aiCompletionService.CompleteStructuredAsync<DraftCommunicationOutput>(promptRequest, cancellationToken);
                rawJson = draftResult.RawJson;
                confidence = draftResult.Confidence;
                modelProvider = draftResult.ModelProvider;
                modelName = draftResult.ModelName;
                promptVersion = draftResult.PromptVersion;
                break;

            default:
                throw new NotSupportedException($"Task type {task.TaskType} not supported.");
        }

        var reviewStatus = ConfidencePolicyEngine.EvaluateReviewStatus(task.TaskType, confidence, isFinancialOrLegal: false);
        task.Complete(rawJson, confidence, modelProvider, modelName, promptVersion, reviewStatus, _timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
        _logCompleted(_logger, task.Id, task.TaskType, confidence, reviewStatus, null);
    }
}
