namespace DgDevelopment.Identity.Domain.Entities;

public sealed class Event
{
    public Guid Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public string AggregateType { get; private set; }
    public string EventType { get; private set; }
    public string Data { get; private set; }
    public int Version { get; private set; }
    public DateTime Timestamp { get; private set; }

    private Event() { }

    public Event(Guid aggregateId, string aggregateType, string eventType, object data, int version)
    {
        Id = Guid.NewGuid();
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        EventType = eventType;
        Data = System.Text.Json.JsonSerializer.Serialize(data);
        Version = version;
        Timestamp = DateTime.UtcNow;
    }
}
