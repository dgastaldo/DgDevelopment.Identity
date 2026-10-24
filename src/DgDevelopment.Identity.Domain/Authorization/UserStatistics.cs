namespace DgDevelopment.Identity.Domain.Authorization;

public sealed record UserStatistics(int Total, int Active, int Locked, int CreatedLast30Days, int MfaEnabled);
