using MngExtensions.Domain;

namespace MngExtensions.Application.Pdks;

public interface IPdksService
{
    Task<IssueCodeResponse> IssueCodeAsync(string doorKey, string? kioskToken, CancellationToken cancellationToken);

    Task<DoorBoardResponse> GetBoardAsync(string doorKey, string? kioskToken, CancellationToken cancellationToken);

    Task<ConsumeCodeResponse> ConsumeAsync(string? nonce, PdksActor actor, CancellationToken cancellationToken, string? photo = null, string? action = null);

    Task<HoldCodeResponse> HoldAsync(string? nonce, CancellationToken cancellationToken);

    Task<PassagePreviewResponse> PreviewAsync(string? nonce, string? publicKey, string? bearer, PdksActor actor, CancellationToken cancellationToken);

    Task<ConsumeCodeResponse> ConfirmAsync(string? nonce, string? publicKey, string? signature, string? photo, string? bearer, PdksActor actor, CancellationToken cancellationToken, string? action = null);

    Task<ScreenListResponse> ListLocationsAsync(PdksActor actor, CancellationToken cancellationToken);

    Task<LiveStatusResponse> GetLiveAsync(PdksActor actor, string? locationKey, long? knownStamp, CancellationToken cancellationToken);

    Task<TimesheetResponse> GetReportAsync(PdksActor actor, string? from, string? to, string? locationKey, CancellationToken cancellationToken);

    Task<ScreenListResponse> ListDoorsAsync(PdksActor actor, string locationKey, CancellationToken cancellationToken);

    Task<BindScreenResponse> BindScreenAsync(PdksActor actor, string doorKey, CancellationToken cancellationToken);
}

public sealed record PdksActor(string UserId, string DisplayName, string? DomainName, IReadOnlyList<string> Groups, bool CanManage = false, string FullName = "");

public sealed class IssueCodeResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string? Nonce { get; init; }
    public string? Url { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? DoorTitle { get; init; }
    public DoorWelcome? Welcome { get; init; }
    public long BoardStamp { get; init; }
}

public sealed class DoorBoardResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<DoorBoardCard> Cards { get; init; } = Array.Empty<DoorBoardCard>();
    public IReadOnlyList<DoorBoardSlice> Priority { get; init; } = Array.Empty<DoorBoardSlice>();
    public IReadOnlyList<DoorBoardSlice> Days { get; init; } = Array.Empty<DoorBoardSlice>();
    public IReadOnlyList<DoorBoardSlice> States { get; init; } = Array.Empty<DoorBoardSlice>();
    public IReadOnlyList<DoorBoardRow> Workspaces { get; init; } = Array.Empty<DoorBoardRow>();
    public IReadOnlyList<DoorAttendanceRow> Day { get; init; } = Array.Empty<DoorAttendanceRow>();
    public IReadOnlyList<DoorBreakRow> Breaks { get; init; } = Array.Empty<DoorBreakRow>();
    public int RefreshSeconds { get; init; } = 60;
    public long Stamp { get; init; }
}

public sealed class DoorBoardCard
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Value { get; init; }
}

public sealed class DoorBoardSlice
{
    public string Label { get; init; } = string.Empty;
    public int Value { get; init; }
    public string? Color { get; init; }
}

public sealed class DoorBoardRow
{
    public string Name { get; init; } = string.Empty;
    public int Open { get; init; }
    public int Closed { get; init; }
}

public sealed class DoorAttendanceRow
{
    public string Name { get; init; } = string.Empty;
    public string Entry { get; init; } = string.Empty;
    public string Exit { get; init; } = string.Empty;
}

public sealed class DoorBreakRow
{
    public string Name { get; init; } = string.Empty;
    public string Started { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
}

public sealed class WorkPulseRow
{
    public string? WorkspaceId { get; init; }
    public string? PriorityId { get; init; }
    public string? StateId { get; init; }
    public DateTime? ClosedAt { get; init; }
}

public sealed class PassagePulseRow
{
    public string? PersonId { get; init; }
    public string? Name { get; init; }
    public string? Direction { get; init; }
    public string? LocationKey { get; init; }
    public string? DoorKey { get; init; }
    public string? Sinif { get; init; }
    public DateTime? OccurredAt { get; init; }
}

public sealed class LivePersonRow
{
    public string Name { get; init; } = string.Empty;
    public string Entry { get; init; } = string.Empty;
    public string Exit { get; init; } = string.Empty;
    public string Started { get; init; } = string.Empty;
    public DateTime? StartedAt { get; init; }
}

public sealed class LiveStatusResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public long Stamp { get; init; }
    public bool Unchanged { get; init; }
    public string LocationKey { get; init; } = string.Empty;
    public DoorBoardCard[] Cards { get; init; } = [];
    public LivePersonRow[] Inside { get; init; } = [];
    public LivePersonRow[] Breaks { get; init; } = [];
    public LivePersonRow[] Left { get; init; } = [];
}

public sealed class CatalogEntry
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public int Sort { get; init; }
}

public sealed class DoorWelcome
{
    public string DisplayName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Greeting { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
}

public sealed class RecentPassage
{
    public string DisplayName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Greeting { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string DoorTitle { get; init; } = string.Empty;
}

public sealed class ConsumeCodeResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string Result { get; init; } = "rejected";
    public string? RejectReason { get; init; }
    public string? Direction { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? DisplayName { get; init; }
    public string? DoorKey { get; init; }
    public string? DoorTitle { get; init; }
    public string? Greeting { get; init; }
}

public interface IPasscodeStore
{
    Task StoreAsync(string nonce, string doorKey, DateTime expiresAtUtc, TimeSpan retention, CancellationToken cancellationToken);

    Task SetWelcomeAsync(string doorKey, string payload, TimeSpan ttl, CancellationToken cancellationToken);

    Task<string?> GetWelcomeAsync(string doorKey, CancellationToken cancellationToken);

    Task SetBoardAsync(string doorKey, string payload, TimeSpan ttl, CancellationToken cancellationToken);

    Task<string?> GetBoardAsync(string doorKey, CancellationToken cancellationToken);

    Task<long> ReadBoardStampAsync(CancellationToken cancellationToken);

    Task AdvanceBoardStampAsync(CancellationToken cancellationToken);

    Task<PasscodeHold> HoldAsync(string nonce, DateTime nowUtc, TimeSpan hold, CancellationToken cancellationToken);

    Task<PasscodePeek> PeekAsync(string nonce, CancellationToken cancellationToken);

    Task<PasscodeTake> TakeAsync(string nonce, TimeSpan tombstoneTtl, CancellationToken cancellationToken);
}

public enum PasscodeTakeKind
{
    Missing,
    Taken,
    AlreadyUsed
}

public sealed record PasscodeTake(PasscodeTakeKind Kind, string? DoorKey, DateTime? ExpiresAtUtc);

public enum PasscodeHoldKind
{
    Missing,
    Expired,
    Used,
    Held
}

public sealed record PasscodeHold(PasscodeHoldKind Kind, DateTime? ExpiresAtUtc);

public sealed record PasscodePeek(string? DoorKey, DateTime? ExpiresAtUtc);

public sealed class PassagePreviewResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string Status { get; init; } = "missing";
    public string? LocationTitle { get; init; }
    public string? DoorTitle { get; init; }
    public string? PersonName { get; init; }
    public string? Direction { get; init; }
    public string? BreakAction { get; init; }
    public bool PhotoRequired { get; init; }
}

public sealed class HoldCodeResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string Status { get; init; } = "missing";
    public DateTime? ExpiresAt { get; init; }
}

public interface IKeeperTokenSource
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public interface IPdksDataGateway
{
    Task<DoorRecord?> FindDoorAsync(string token, string doorKey, CancellationToken cancellationToken);

    Task<PassageRecord?> FindLastAcceptedAsync(string token, string personId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PassageRecord>> ListAcceptedForPersonAsync(string token, string personId, int limit, CancellationToken cancellationToken);

    Task<int> ExpirePhotosAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<PassageRecord>> ListRecentAcceptedAsync(string token, string doorKey, int limit, CancellationToken cancellationToken);

    Task WritePassageAsync(string token, PassageWrite write, CancellationToken cancellationToken);

    Task<GreetingSchedule> FindScheduleAsync(string token, string doorKey, CancellationToken cancellationToken);

    Task<string?> FindDashboardSlugAsync(string token, string doorKey, CancellationToken cancellationToken);

    Task<int> CountAsync(string token, string dataset, string? filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkPulseRow>> ListWorkPulseAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<PassagePulseRow>> ListPassagesSinceAsync(string token, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<DoorClockBook> ListDoorClocksAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<RosterPerson>> ListRosterAsync(string token, string groupName, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogEntry>> ListCatalogAsync(string token, string dataset, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScreenPlace>> ListActiveLocationsAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScreenPlace>> ListActiveDoorsAsync(string token, string locationKey, CancellationToken cancellationToken);

    Task<BindScreenResponse?> ReplaceKioskTokenAsync(string token, string doorKey, string kioskToken, CancellationToken cancellationToken);
}

public sealed class DoorRecord
{
    public string DoorKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? DirectionMode { get; init; }
    public bool Active { get; init; }
    public string? KioskTokenHash { get; init; }
    public string? LocationKey { get; init; }
}

public sealed class PassageRecord
{
    public string? Direction { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? DisplayName { get; init; }
    public string? FullName { get; init; }
    public string? Greeting { get; init; }
    public string? DoorKey { get; init; }
}

public sealed class PassageWrite
{
    public string PersonId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DoorKey { get; init; } = string.Empty;
    public string? LocationKey { get; init; }
    public string Direction { get; init; } = "in";
    public DateTime OccurredAt { get; init; }
    public string Result { get; init; } = "rejected";
    public string? RejectReason { get; init; }
    public string? FullName { get; init; }
    public string? Sinif { get; init; }
    public string? Greeting { get; init; }
    public PassagePhotoFile? Photo { get; init; }
    public DateTime? PhotoExpiresAt { get; init; }
    public string? PhotoState { get; init; }
}

public sealed class PassagePhotoFile
{
    public string Content { get; init; } = string.Empty;
    public string Folder { get; init; } = "gecis";
    public bool UseCompression { get; init; }
    public bool UseEncryption { get; init; } = true;
}

public sealed class GreetingSchedule
{
    public string EntryClock { get; init; } = "09:00";
    public string ExitClock { get; init; } = "18:00";
    public int ToleranceMinutes { get; init; }
    public string EntryPhrase { get; init; } = "İyi çalışmalar.";
    public string OnTimePhrase { get; init; } = "Zamanında geldiğiniz için teşekkürler.";
    public string LatePhrase { get; init; } = "Yolunuz düştü, yine de hoş geldiniz.";
    public string ExitPhrase { get; init; } = "Güle güle.";
    public int BoardRefreshSeconds { get; init; } = 60;
}

public sealed class ScreenPlace
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

public sealed class ScreenListResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public IReadOnlyList<ScreenPlace> Items { get; init; } = Array.Empty<ScreenPlace>();
}

public sealed class BindScreenResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string? DoorKey { get; init; }
    public string? DoorTitle { get; init; }
    public string? LocationKey { get; init; }
    public string? LocationTitle { get; init; }
    public string? KioskToken { get; init; }
}

public sealed class RosterPerson
{
    public string PersonId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class DoorClockBook
{
    public AttendanceClock Fallback { get; init; } = new("09:00", "18:00", 15);
    public IReadOnlyDictionary<string, AttendanceClock> ByDoor { get; init; } = new Dictionary<string, AttendanceClock>();
}

public sealed class TimesheetDayRow
{
    public string Date { get; init; } = string.Empty;
    public bool Absent { get; init; }
    public bool Open { get; init; }
    public string Entry { get; init; } = string.Empty;
    public string EntryClass { get; init; } = string.Empty;
    public string Exit { get; init; } = string.Empty;
    public string ExitClass { get; init; } = string.Empty;
    public int BreakCount { get; init; }
    public int BreakMinutes { get; init; }
    public int? WorkMinutes { get; init; }
}

public sealed class TimesheetPersonRow
{
    public string PersonId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int CameDays { get; init; }
    public int LateEntries { get; init; }
    public int EarlyExits { get; init; }
    public int LateExits { get; init; }
    public int BreakCount { get; init; }
    public int BreakMinutes { get; init; }
    public int WorkMinutes { get; init; }
    public bool Open { get; init; }
    public TimesheetDayRow[] Days { get; init; } = [];
}

public sealed class TimesheetResponse
{
    public int StatusCode { get; init; } = 200;
    public string? Message { get; init; }
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string LocationKey { get; init; } = string.Empty;
    public TimesheetPersonRow[] People { get; init; } = [];
}

public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken);
}

public sealed class HealthCheckResult
{
    public string Status { get; init; } = "healthy";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Redis { get; init; } = "unknown";
}
