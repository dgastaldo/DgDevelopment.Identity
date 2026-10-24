namespace DgDevelopment.Identity.Application.Consent;

public sealed class ConsentOptions
{
    public const string SectionName = "Identity";

    public int ConsentLifetimeDays { get; set; } = 180;
}