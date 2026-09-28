namespace MeetSync.Application.Common.Options;

public class MeetSyncSettings
{
    public const string SectionName = "MeetSyncSettings";

    public int MaxParticipantsPerRoom { get; set; } = 50;
    public bool EnableScreenSharing { get; set; } = true;
    public string StunServerUrl { get; set; } = "stun:stun.l.google.com:19302";
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool UseRedisStateStore { get; set; } = false;
    public string RedisConnectionString { get; set; } = "localhost:6379";
}
