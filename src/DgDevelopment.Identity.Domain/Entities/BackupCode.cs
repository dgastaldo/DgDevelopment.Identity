namespace DgDevelopment.Identity.Domain.Entities;

public sealed class BackupCode
{
    public Guid Id { get; private set; }
    public Guid TotpSecretId { get; private set; }
    public string CodeHash { get; private set; }
    public bool IsUsed { get; private set; }

    private BackupCode() { }

    public BackupCode(Guid totpSecretId, string codeHash)
    {
        Id = Guid.NewGuid();
        TotpSecretId = totpSecretId;
        CodeHash = codeHash;
        IsUsed = false;
    }

    internal void MarkUsed() => IsUsed = true;
}
