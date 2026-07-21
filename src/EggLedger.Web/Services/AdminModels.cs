namespace EggLedger.Web.Services;

public sealed record AdminUser(Guid UserId, string? DiscordId, string Username, string AvatarUrl, long MissionCount, long BackupCount, long ReportCount, long StorageBytes, long? LastSession, bool IsAdmin);

public sealed record AdminMetrics(IReadOnlyList<AdminMinute> Minutes, IReadOnlyList<AdminPath> Paths, IReadOnlyList<AdminSpam> Spam);

public sealed record AdminMinute(long MinuteEpochSeconds, int Total);

public sealed record AdminPath(string Path, long Count);

public sealed record AdminSpam(string Ip, string Method, string Path, string UserAgent, long FirstSeen, long LastSeen, long Hits);
