namespace ResolveOps.Messaging.Events;

public sealed record AiTaskRequestedV1(
    Guid TaskId,
    Guid TenantId,
    string TaskType,
    string SourceEntityType,
    Guid SourceEntityId,
    string InputText,
    Guid? CarrierId
) : IIntegrationEvent
{
    public string EventType => "AiTaskRequestedV1";
    public int EventVersion => 1;
}
