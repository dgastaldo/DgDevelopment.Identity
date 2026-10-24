namespace DgDevelopment.Identity.Domain.Entities;

public enum PushPlatform
{
    Android,
    Apple,
    Windows
}

public sealed class PushDevice
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public PushPlatform Platform { get; private set; }
    public string PushToken { get; private set; }
    public string? DeviceName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    private PushDevice() { }

    public PushDevice(Guid userId, PushPlatform platform, string pushToken, string? deviceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pushToken);
        Id = Guid.NewGuid();
        UserId = userId;
        Platform = platform;
        PushToken = pushToken;
        DeviceName = deviceName;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
    }

    public void UpdateToken(string pushToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pushToken);
        PushToken = pushToken;
        LastSeenAt = DateTime.UtcNow;
    }

    public void MarkSeen()
    {
        LastSeenAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}