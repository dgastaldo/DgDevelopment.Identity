namespace DgDevelopment.Identity.Domain.Entities;

public sealed class TotpSecret
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string SecretKey { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? EnabledAt { get; private set; }

    private readonly List<BackupCode> _backupCodes = [];
    public IReadOnlyCollection<BackupCode> BackupCodes => _backupCodes.AsReadOnly();

    private TotpSecret() { }

    public TotpSecret(Guid userId, string secretKey)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        SecretKey = secretKey;
        IsEnabled = false;
    }

    public void Enable()
    {
        IsEnabled = true;
        EnabledAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        EnabledAt = null;
    }

    public void AddBackupCode(string codeHash)
    {
        _backupCodes.Add(new BackupCode(Id, codeHash));
    }

    public BackupCode? RedeemBackupCode(string codeHash)
    {
        var code = _backupCodes.FirstOrDefault(c => c.CodeHash == codeHash && !c.IsUsed);
        if (code != null)
            code.MarkUsed();

        return code;
    }

    public int AvailableBackupCodes() => _backupCodes.Count(c => !c.IsUsed);
}
