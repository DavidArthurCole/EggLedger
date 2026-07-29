namespace EggLedger.Web.Services;

public sealed record AdminUser(Guid UserId, string? DiscordId, string Username, string AvatarUrl, long MissionCount, long BackupCount, long ReportCount, long StorageBytes, long? LastSession, bool IsAdmin);
