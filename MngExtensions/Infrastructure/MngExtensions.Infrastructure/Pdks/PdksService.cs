using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngExtensions.Application.Configuration;
using MngExtensions.Application.Pdks;
using MngExtensions.Domain;

namespace MngExtensions.Infrastructure.Pdks;

public sealed class PdksService : IPdksService
{
    private readonly IPasscodeStore _passcodes;
    private readonly IKeeperTokenSource _tokens;
    private readonly IPdksDataGateway _data;
    private readonly IHttpClientFactory _http;
    private readonly IOptions<MngExtensionsSettings> _settings;
    private readonly ILogger<PdksService> _logger;
    private readonly TimeZoneInfo _istanbul = PassageRules.IstanbulClock();

    public PdksService(
        IPasscodeStore passcodes,
        IKeeperTokenSource tokens,
        IPdksDataGateway data,
        IHttpClientFactory http,
        IOptions<MngExtensionsSettings> settings,
        ILogger<PdksService> logger)
    {
        _passcodes = passcodes;
        _tokens = tokens;
        _data = data;
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IssueCodeResponse> IssueCodeAsync(string doorKey, string? kioskToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doorKey) || string.IsNullOrWhiteSpace(kioskToken))
            return UndefinedDoor();

        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var door = await _data.FindDoorAsync(token, doorKey.Trim(), cancellationToken);
            if (door is null || !PassageRules.HashesMatch(door.KioskTokenHash ?? string.Empty, kioskToken))
                return UndefinedDoor();
            if (!door.Active)
                return new IssueCodeResponse { StatusCode = 409, Message = "Kapı pasif." };

            var pdks = _settings.Value.Pdks;
            var now = DateTime.UtcNow;
            var expiresAt = now.AddSeconds(Math.Max(1, pdks.CodeSeconds));
            var nonce = NewNonce();
            var retention = TimeSpan.FromSeconds(Math.Max(pdks.CodeSeconds + 5, pdks.NonceRetentionSeconds));
            await _passcodes.StoreAsync(nonce, door.DoorKey, expiresAt, retention, cancellationToken);

            return new IssueCodeResponse
            {
                Nonce = nonce,
                Url = BuildPassUrl(nonce),
                ExpiresAt = expiresAt,
                DoorTitle = door.Title,
                Welcome = await ReadWelcomeAsync(door.DoorKey, cancellationToken),
                BoardStamp = await _passcodes.ReadBoardStampAsync(cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Code issue failed for door {DoorKey}", doorKey);
            return new IssueCodeResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<DoorBoardResponse> GetBoardAsync(string doorKey, string? kioskToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doorKey) || string.IsNullOrWhiteSpace(kioskToken))
            return new DoorBoardResponse { StatusCode = 404, Message = "Kapı tanımsız." };

        var key = doorKey.Trim();
        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var door = await _data.FindDoorAsync(token, key, cancellationToken);
            if (door is null || !PassageRules.HashesMatch(door.KioskTokenHash ?? string.Empty, kioskToken))
                return new DoorBoardResponse { StatusCode = 404, Message = "Kapı tanımsız." };

            var stamp = await _passcodes.ReadBoardStampAsync(cancellationToken);
            var cached = await _passcodes.GetBoardAsync(door.DoorKey, cancellationToken);
            var fromCache = ReadBoard(cached);
            if (fromCache is not null && fromCache.Stamp == stamp)
                return fromCache;

            var slug = await _data.FindDashboardSlugAsync(token, door.DoorKey, cancellationToken);
            var schedule = await _data.FindScheduleAsync(token, door.DoorKey, cancellationToken);
            var refresh = ClampBoardRefresh(schedule.BoardRefreshSeconds);
            var built = string.Equals(slug, "kapi-is-ozeti", StringComparison.Ordinal)
                ? await BuildAttendanceBoardAsync(token, cancellationToken)
                : new DoorBoardResponse();
            var board = WithRefresh(built, refresh, stamp);

            try
            {
                await _passcodes.SetBoardAsync(door.DoorKey, JsonSerializer.Serialize(board), TimeSpan.FromSeconds(refresh), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Door board cache failed for {DoorKey}", door.DoorKey);
            }

            return board;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Door board failed for {DoorKey}", key);
            return new DoorBoardResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<HoldCodeResponse> HoldAsync(string? nonce, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return new HoldCodeResponse { Status = "missing" };

        try
        {
            var seconds = Math.Max(1, _settings.Value.Pdks.PasswordHoldSeconds);
            var held = await _passcodes.HoldAsync(nonce.Trim(), DateTime.UtcNow, TimeSpan.FromSeconds(seconds), cancellationToken);
            return held.Kind switch
            {
                PasscodeHoldKind.Held => new HoldCodeResponse { Status = "held", ExpiresAt = held.ExpiresAtUtc },
                PasscodeHoldKind.Expired => new HoldCodeResponse { Status = "expired" },
                PasscodeHoldKind.Used => new HoldCodeResponse { Status = "used" },
                _ => new HoldCodeResponse { Status = "missing" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passcode hold failed");
            return new HoldCodeResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<PassagePreviewResponse> PreviewAsync(
        string? nonce,
        string? publicKey,
        string? bearer,
        PdksActor actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return new PassagePreviewResponse { Status = "missing", Message = "Kod tanınmadı." };

        var approved = await ReadApprovedNameAsync(publicKey, bearer, cancellationToken);
        if (approved.StatusCode != 200)
            return new PassagePreviewResponse { StatusCode = approved.StatusCode, Status = "unapproved", Message = approved.Message };

        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            if (await DayIsCompleteAsync(token, actor.UserId, cancellationToken))
                return await CompletedDayAsync(token, nonce, approved.Name, actor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Day check failed");
            return new PassagePreviewResponse { StatusCode = 503, Status = "offline", Message = "Bağlantı yok." };
        }

        var held = await HoldAsync(nonce, cancellationToken);
        if (held.StatusCode != 200)
            return new PassagePreviewResponse { StatusCode = held.StatusCode, Status = "offline", Message = held.Message ?? "Bağlantı yok." };
        if (held.Status != "held")
            return new PassagePreviewResponse { Status = held.Status, Message = HoldMessage(held.Status) };

        try
        {
            var peeked = await _passcodes.PeekAsync(nonce.Trim(), cancellationToken);
            if (string.IsNullOrWhiteSpace(peeked.DoorKey))
                return new PassagePreviewResponse { Status = "missing", Message = "Kod tanınmadı." };

            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var door = await _data.FindDoorAsync(token, peeked.DoorKey, cancellationToken);
            if (door is null)
                return new PassagePreviewResponse { Status = "tanimsiz", Message = "Kapı tanımsız." };
            if (!door.Active)
                return new PassagePreviewResponse { Status = "pasif", DoorTitle = door.Title, Message = "Kapı pasif." };

            var places = await _data.ListActiveLocationsAsync(token, cancellationToken);
            var location = places.FirstOrDefault(item => string.Equals(item.Key, door.LocationKey, StringComparison.Ordinal));
            var recent = await _data.ListAcceptedForPersonAsync(token, actor.UserId, 50, cancellationToken);
            var now = DateTime.UtcNow;
            var last = recent.FirstOrDefault(row => PassageRules.IsWork(row.Direction));
            var breakOpen = PassageRules.OpenBreakStart(recent.Select(row => (row.Direction, row.OccurredAt)), now, _istanbul) is not null;
            var direction = PassageRules.ResolveDirection(door.DirectionMode, last?.Direction, last?.OccurredAt, now, _istanbul);
            var person = string.IsNullOrWhiteSpace(approved.Name) ? actor.FullName : approved.Name;
            return new PassagePreviewResponse
            {
                Status = "ready",
                LocationTitle = location?.Title ?? door.LocationKey,
                DoorTitle = door.Title,
                PersonName = string.IsNullOrWhiteSpace(person) ? actor.DisplayName : person,
                Direction = direction,
                BreakAction = PassageRules.BreakChoice(last?.Direction, last?.OccurredAt, now, _istanbul, breakOpen),
                PhotoRequired = approved.PhotoRequired
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passage preview failed");
            return new PassagePreviewResponse { StatusCode = 503, Status = "offline", Message = "Bağlantı yok." };
        }
    }

    public async Task<ConsumeCodeResponse> ConfirmAsync(
        string? nonce,
        string? publicKey,
        string? signature,
        string? photo,
        string? bearer,
        PdksActor actor,
        CancellationToken cancellationToken,
        string? action = null)
    {
        var approved = await ReadApprovedNameAsync(publicKey, bearer, cancellationToken);
        if (approved.StatusCode != 200)
            return new ConsumeCodeResponse { StatusCode = approved.StatusCode, Result = "rejected", Message = approved.Message, RejectReason = "yetki" };

        if (!VerifySignature(publicKey!, nonce ?? string.Empty, signature))
            return new ConsumeCodeResponse { Result = "rejected", Message = "Anahtar imzası geçersiz.", RejectReason = "imza" };

        string? frame = null;
        if (approved.PhotoRequired)
        {
            frame = PassageFrame.NormalizeJpeg(photo);
            if (frame is null)
                return new ConsumeCodeResponse { StatusCode = 400, Result = "rejected", Message = "Fotoğraf gerekli.", RejectReason = "foto" };
        }

        return await ConsumeAsync(nonce, actor, cancellationToken, frame, action);
    }

    public async Task<ConsumeCodeResponse> ConsumeAsync(string? nonce, PdksActor actor, CancellationToken cancellationToken, string? photo = null, string? action = null)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return Rejected(null, null, null, "bilinmiyor");

        try
        {
            var serviceToken = await _tokens.GetAccessTokenAsync(cancellationToken);
            if (await DayIsCompleteAsync(serviceToken, actor.UserId, cancellationToken))
                return new ConsumeCodeResponse { StatusCode = 409, Result = "rejected", Message = "Bugünkü geçiş tamam." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Day check failed");
            return new ConsumeCodeResponse { StatusCode = 503, Message = "Bağlantı yok.", Result = "rejected" };
        }

        PasscodeTake taken;
        try
        {
            var retention = TimeSpan.FromSeconds(Math.Max(30, _settings.Value.Pdks.NonceRetentionSeconds));
            taken = await _passcodes.TakeAsync(nonce.Trim(), retention, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passcode store failed");
            return new ConsumeCodeResponse { StatusCode = 503, Message = "Bağlantı yok.", Result = "rejected" };
        }

        if (taken.Kind == PasscodeTakeKind.Missing || string.IsNullOrWhiteSpace(taken.DoorKey))
            return Rejected(null, null, null, "bilinmiyor");

        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var door = await _data.FindDoorAsync(token, taken.DoorKey, cancellationToken);
            var now = DateTime.UtcNow;
            var title = door?.Title ?? taken.DoorKey;

            if (taken.Kind == PasscodeTakeKind.AlreadyUsed)
                return await WriteAndReturn(token, actor, taken.DoorKey, title, door?.LocationKey, "in", now, "rejected", "kullanilmis", cancellationToken);

            if (taken.ExpiresAtUtc is null || taken.ExpiresAtUtc <= now)
                return await WriteAndReturn(token, actor, taken.DoorKey, title, door?.LocationKey, "in", now, "rejected", "sure", cancellationToken);

            if (door is null)
                return Rejected(taken.DoorKey, null, now, "tanimsiz");

            if (!door.Active)
                return await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, "in", now, "rejected", "pasif", cancellationToken);

            var accountDomain = _settings.Value.ServiceAccount.DomainName;
            var group = _settings.Value.Pdks.AttendanceGroup;
            var allowed = !string.IsNullOrWhiteSpace(group)
                && string.Equals(actor.DomainName, accountDomain, StringComparison.OrdinalIgnoreCase)
                && actor.Groups.Any(item => string.Equals(item, group, StringComparison.OrdinalIgnoreCase));
            if (!allowed || string.IsNullOrWhiteSpace(actor.UserId))
                return await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, "in", now, "rejected", "yetki", cancellationToken);

            var recent = await _data.ListAcceptedForPersonAsync(token, actor.UserId, 50, cancellationToken);
            var last = recent.FirstOrDefault(row => PassageRules.IsWork(row.Direction));
            var breakOpen = PassageRules.OpenBreakStart(recent.Select(row => (row.Direction, row.OccurredAt)), now, _istanbul) is not null;
            var direction = PassageRules.ChooseAction(door.DirectionMode, last?.Direction, last?.OccurredAt, breakOpen, now, _istanbul, action);
            if (direction is null)
                return await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, "in", now, "rejected", "islem", cancellationToken);

            var lastSame = recent.FirstOrDefault(row => string.Equals(row.Direction, direction, StringComparison.OrdinalIgnoreCase));
            if (PassageRules.IsDuplicate(lastSame?.OccurredAt, now, _settings.Value.Pdks.DuplicateSeconds))
                return await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, direction, now, "ignored", null, cancellationToken);

            if (!PassageRules.IsWork(direction))
            {
                var phrase = string.Equals(direction, "break", StringComparison.OrdinalIgnoreCase)
                    ? PassageRules.BreakGreeting
                    : PassageRules.ReturnGreeting;
                return await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, direction, now, "accepted", null, cancellationToken, null, phrase, photo);
            }

            if (string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase) && breakOpen)
                await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, "return", now, "accepted", null, cancellationToken, null, PassageRules.BreakClosedByExit, null, false);

            var schedule = await _data.FindScheduleAsync(token, door.DoorKey, cancellationToken);
            var clock = string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase) ? schedule.ExitClock : schedule.EntryClock;
            var sinif = PassageRules.Classify(now, clock, schedule.ToleranceMinutes, _istanbul);
            var greeting = PassageRules.ChooseGreeting(direction, sinif, schedule.EntryPhrase, schedule.OnTimePhrase, schedule.LatePhrase, schedule.ExitPhrase);
            var occurred = string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase) && breakOpen ? now.AddMilliseconds(1) : now;
            return await WriteAndReturn(token, actor, door.DoorKey, title, door.LocationKey, direction, occurred, "accepted", null, cancellationToken, sinif, greeting, photo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passage consume failed for door {DoorKey}", taken.DoorKey);
            return new ConsumeCodeResponse { StatusCode = 503, Message = "Bağlantı yok.", Result = "rejected", DoorKey = taken.DoorKey };
        }
    }

    private async Task<ConsumeCodeResponse> WriteAndReturn(
        string token,
        PdksActor actor,
        string doorKey,
        string doorTitle,
        string? locationKey,
        string direction,
        DateTime occurredAt,
        string result,
        string? rejectReason,
        CancellationToken cancellationToken,
        string? sinif = null,
        string? greeting = null,
        string? photo = null,
        bool announce = true)
    {
        var name = string.IsNullOrWhiteSpace(actor.DisplayName) ? "Personel" : actor.DisplayName;
        await _data.WritePassageAsync(token, new PassageWrite
        {
            PersonId = string.IsNullOrWhiteSpace(actor.UserId) ? "unknown" : actor.UserId,
            DisplayName = name,
            FullName = string.IsNullOrWhiteSpace(actor.FullName) ? name : actor.FullName,
            Sinif = sinif,
            Greeting = greeting,
            Photo = result == "accepted" && !string.IsNullOrWhiteSpace(photo)
                ? new PassagePhotoFile { Content = photo, Folder = "gecis", UseCompression = false, UseEncryption = true }
                : null,
            PhotoExpiresAt = result == "accepted" && !string.IsNullOrWhiteSpace(photo) ? occurredAt.Add(PassageFrame.Retention) : null,
            PhotoState = result == "accepted" && !string.IsNullOrWhiteSpace(photo) ? PassageFrame.Live : null,
            DoorKey = doorKey,
            LocationKey = locationKey,
            Direction = direction,
            OccurredAt = occurredAt,
            Result = result,
            RejectReason = rejectReason
        }, cancellationToken);

        if (result == "accepted")
        {
            try
            {
                await _passcodes.AdvanceBoardStampAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Board stamp failed for door {DoorKey}", doorKey);
            }
        }

        if (announce && result == "accepted" && !string.IsNullOrWhiteSpace(greeting))
        {
            try
            {
                var payload = JsonSerializer.Serialize(new DoorWelcome
                {
                    DisplayName = name,
                    FullName = string.IsNullOrWhiteSpace(actor.FullName) ? name : actor.FullName,
                    Greeting = greeting,
                    Direction = direction
                });
                await _passcodes.SetWelcomeAsync(doorKey, payload, TimeSpan.FromSeconds(8), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Welcome could not be published for door {DoorKey}", doorKey);
            }
        }

        return new ConsumeCodeResponse
        {
            Result = result,
            RejectReason = rejectReason,
            Direction = direction,
            OccurredAt = occurredAt,
            DisplayName = actor.DisplayName,
            DoorKey = doorKey,
            DoorTitle = doorTitle,
            Greeting = greeting
        };
    }

    public async Task<LiveStatusResponse> GetLiveAsync(PdksActor actor, string? locationKey, long? knownStamp, CancellationToken cancellationToken)
    {
        var denied = DenyLive(actor);
        if (denied is not null)
            return new LiveStatusResponse { StatusCode = denied.Value.Status, Message = denied.Value.Message };
        if (string.IsNullOrWhiteSpace(locationKey))
            return new LiveStatusResponse { StatusCode = 400, Message = "Lokasyon seçin." };

        var place = locationKey.Trim();
        try
        {
            var stamp = await _passcodes.ReadBoardStampAsync(cancellationToken);
            if (knownStamp is not null && knownStamp.Value == stamp)
                return new LiveStatusResponse { Stamp = stamp, Unchanged = true, LocationKey = place };

            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istanbul);
            var todayLocal = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
            var todayUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, _istanbul);
            var passages = await _data.ListPassagesSinceAsync(token, todayUtc, cancellationToken);
            var people = PassageRules.Live(
                passages.Select(row => (row.PersonId, row.Name, row.Direction, row.OccurredAt, row.LocationKey)),
                DateTime.UtcNow,
                _istanbul);

            var inside = people.Where(person => PassageRules.InsideAt(person, place))
                .OrderBy(person => person.EntryUtc)
                .ThenBy(person => person.Name, StringComparer.CurrentCulture)
                .Select(person => PersonRow(person, includeExit: false, includeBreak: false))
                .ToArray();
            var breaks = people.Where(person => PassageRules.OnBreakAt(person, place))
                .OrderBy(person => person.BreakStartUtc)
                .ThenBy(person => person.Name, StringComparer.CurrentCulture)
                .Select(person => PersonRow(person, includeExit: false, includeBreak: true))
                .ToArray();
            var left = people.Where(person => PassageRules.LeftAt(person, place))
                .OrderByDescending(person => person.ExitUtc)
                .ThenBy(person => person.Name, StringComparer.CurrentCulture)
                .Select(person => PersonRow(person, includeExit: true, includeBreak: false))
                .ToArray();

            return new LiveStatusResponse
            {
                Stamp = stamp,
                LocationKey = place,
                Cards =
                [
                    new DoorBoardCard { Key = "arrived", Label = "Bugün gelen", Value = people.Count(person => PassageRules.ArrivedAt(person, place)) },
                    new DoorBoardCard { Key = "inside", Label = "İçeride", Value = inside.Length },
                    new DoorBoardCard { Key = "break", Label = "Molada", Value = breaks.Length },
                    new DoorBoardCard { Key = "left", Label = "Çıkan", Value = left.Length }
                ],
                Inside = inside,
                Breaks = breaks,
                Left = left
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live status failed for location {LocationKey}", place);
            return new LiveStatusResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<TimesheetResponse> GetReportAsync(PdksActor actor, string? fromText, string? toText, string? locationKey, CancellationToken cancellationToken)
    {
        var denied = DenyReport(actor);
        if (denied is not null)
            return new TimesheetResponse { StatusCode = denied.Value.Status, Message = denied.Value.Message };

        if (!TryRange(fromText, toText, out var from, out var to, out var rangeError))
            return new TimesheetResponse { StatusCode = 400, Message = rangeError };

        var place = string.IsNullOrWhiteSpace(locationKey) ? string.Empty : locationKey.Trim();
        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var startLocal = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var since = TimeZoneInfo.ConvertTimeToUtc(startLocal, _istanbul);
            var book = await _data.ListDoorClocksAsync(token, cancellationToken);
            var roster = await _data.ListRosterAsync(token, _settings.Value.Pdks.AttendanceGroup, cancellationToken);
            var passages = await _data.ListPassagesSinceAsync(token, since, cancellationToken);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istanbul));
            var until = to > today ? today : to;
            var people = PassageTimesheet.Build(
                roster.Select(person => (person.PersonId, person.Name)),
                passages
                    .Where(row => row.OccurredAt is not null && !string.IsNullOrWhiteSpace(row.PersonId))
                    .Select(row => new TimesheetPass(
                        row.PersonId!,
                        row.Name,
                        row.Direction,
                        DateTime.SpecifyKind(row.OccurredAt!.Value, DateTimeKind.Utc),
                        row.LocationKey,
                        row.DoorKey,
                        row.Sinif)),
                from,
                until,
                place,
                book.ByDoor,
                book.Fallback,
                _istanbul);

            return new TimesheetResponse
            {
                From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LocationKey = place,
                People = people
                    .OrderBy(person => person.Name, StringComparer.CurrentCulture)
                    .Select(MapPerson)
                    .ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Timesheet failed");
            return new TimesheetResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<ScreenListResponse> ListLocationsAsync(PdksActor actor, CancellationToken cancellationToken)
    {
        var denied = DenyManager(actor);
        if (denied is not null)
            return new ScreenListResponse { StatusCode = denied.Value.Status, Message = denied.Value.Message };

        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var items = await _data.ListActiveLocationsAsync(token, cancellationToken);
            return new ScreenListResponse { Items = items };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Location list failed");
            return new ScreenListResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<ScreenListResponse> ListDoorsAsync(PdksActor actor, string locationKey, CancellationToken cancellationToken)
    {
        var denied = DenyManager(actor);
        if (denied is not null)
            return new ScreenListResponse { StatusCode = denied.Value.Status, Message = denied.Value.Message };
        if (string.IsNullOrWhiteSpace(locationKey))
            return new ScreenListResponse { StatusCode = 404, Message = "Lokasyon tanımsız." };

        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var items = await _data.ListActiveDoorsAsync(token, locationKey.Trim(), cancellationToken);
            return new ScreenListResponse { Items = items };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Door list failed for location {LocationKey}", locationKey);
            return new ScreenListResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    public async Task<BindScreenResponse> BindScreenAsync(PdksActor actor, string doorKey, CancellationToken cancellationToken)
    {
        var denied = DenyManager(actor);
        if (denied is not null)
            return new BindScreenResponse { StatusCode = denied.Value.Status, Message = denied.Value.Message };
        if (string.IsNullOrWhiteSpace(doorKey))
            return new BindScreenResponse { StatusCode = 404, Message = "Kapı tanımsız." };

        try
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);
            var raw = NewNonce();
            var bound = await _data.ReplaceKioskTokenAsync(token, doorKey.Trim(), PassageRules.HashKioskToken(raw), cancellationToken);
            if (bound is null)
                return new BindScreenResponse { StatusCode = 404, Message = "Kapı tanımsız." };
            return new BindScreenResponse
            {
                DoorKey = bound.DoorKey,
                DoorTitle = bound.DoorTitle,
                LocationKey = bound.LocationKey,
                LocationTitle = bound.LocationTitle,
                KioskToken = raw
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Screen bind failed for door {DoorKey}", doorKey);
            return new BindScreenResponse { StatusCode = 503, Message = "Bağlantı yok." };
        }
    }

    private static int ClampBoardRefresh(int seconds)
    {
        if (seconds <= 0)
            return 60;
        if (seconds < 15)
            return 15;
        if (seconds > 600)
            return 600;
        return seconds;
    }

    private static DoorBoardResponse WithRefresh(DoorBoardResponse board, int refresh, long stamp) => new()
    {
        StatusCode = board.StatusCode,
        Message = board.Message,
        Title = board.Title,
        Cards = board.Cards,
        Priority = board.Priority,
        Days = board.Days,
        States = board.States,
        Workspaces = board.Workspaces,
        Day = board.Day,
        Breaks = board.Breaks,
        RefreshSeconds = refresh,
        Stamp = stamp
    };

    private async Task<DoorBoardResponse> BuildAttendanceBoardAsync(string token, CancellationToken cancellationToken)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istanbul);
        var todayLocal = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var todayUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, _istanbul);
        var passages = await ReadOrEmptyAsync(() => _data.ListPassagesSinceAsync(token, todayUtc, cancellationToken), "passages");
        var people = PassageRules.Attendance(
            passages.Select(row => (row.PersonId, row.Name, row.Direction, row.OccurredAt)),
            DateTime.UtcNow,
            _istanbul);

        var day = people
            .Where(person => person.EntryUtc is not null)
            .OrderBy(person => person.EntryUtc)
            .ThenBy(person => person.Name, StringComparer.CurrentCulture)
            .Select(person => new DoorAttendanceRow
            {
                Name = string.IsNullOrWhiteSpace(person.Name) ? "Personel" : person.Name,
                Entry = Clock(person.EntryUtc),
                Exit = Clock(person.ExitUtc)
            })
            .ToArray();
        var breaks = people
            .Where(person => person.BreakStartUtc is not null)
            .OrderBy(person => person.BreakStartUtc)
            .ThenBy(person => person.Name, StringComparer.CurrentCulture)
            .Select(person => new DoorBreakRow
            {
                Name = string.IsNullOrWhiteSpace(person.Name) ? "Personel" : person.Name,
                Started = Clock(person.BreakStartUtc),
                StartedAt = DateTime.SpecifyKind(person.BreakStartUtc!.Value, DateTimeKind.Utc)
            })
            .ToArray();

        return new DoorBoardResponse
        {
            Title = "Devam",
            Cards = new[]
            {
                new DoorBoardCard { Key = "arrived", Label = "Bugün gelen", Value = people.Count(person => person.EntryUtc is not null) },
                new DoorBoardCard { Key = "inside", Label = "İçeride", Value = people.Count(person => person.EntryUtc is not null && person.ExitUtc is null && person.BreakStartUtc is null) },
                new DoorBoardCard { Key = "break", Label = "Molada", Value = breaks.Length },
                new DoorBoardCard { Key = "left", Label = "Çıkan", Value = people.Count(person => person.ExitUtc is not null) }
            },
            Day = day,
            Breaks = breaks
        };
    }

    private string Clock(DateTime? utc)
    {
        if (utc is null)
            return string.Empty;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), _istanbul);
        return local.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<T>> ReadOrEmptyAsync<T>(Func<Task<IReadOnlyList<T>>> read, string name)
    {
        try
        {
            return await read();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Board source failed for {Source}", name);
            return Array.Empty<T>();
        }
    }

    private static DoorBoardResponse? ReadBoard(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            return JsonSerializer.Deserialize<DoorBoardResponse>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<DoorWelcome?> ReadWelcomeAsync(string doorKey, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await _passcodes.GetWelcomeAsync(doorKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            return JsonSerializer.Deserialize<DoorWelcome>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Welcome could not be read for door {DoorKey}", doorKey);
            return null;
        }
    }

    private LivePersonRow PersonRow(PassageRules.LivePerson person, bool includeExit, bool includeBreak) => new()
    {
        Name = string.IsNullOrWhiteSpace(person.Name) ? "Personel" : person.Name,
        Entry = Clock(person.EntryUtc),
        Exit = includeExit ? Clock(person.ExitUtc) : string.Empty,
        Started = includeBreak ? Clock(person.BreakStartUtc) : string.Empty,
        StartedAt = includeBreak && person.BreakStartUtc is not null
            ? DateTime.SpecifyKind(person.BreakStartUtc.Value, DateTimeKind.Utc)
            : null
    };

    private bool TryRange(string? fromText, string? toText, out DateOnly from, out DateOnly to, out string message)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istanbul);
        var monthStart = new DateOnly(localNow.Year, localNow.Month, 1);
        if (string.IsNullOrWhiteSpace(fromText) && string.IsNullOrWhiteSpace(toText))
        {
            from = monthStart;
            to = monthStart.AddMonths(1).AddDays(-1);
            message = string.Empty;
            return true;
        }

        var parsedFrom = DateOnly.TryParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from);
        var parsedTo = DateOnly.TryParseExact(toText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to);
        if (!parsedFrom || !parsedTo)
        {
            message = "Tarih aralığı seçin.";
            return false;
        }

        if (to < from)
        {
            message = "Başlangıç, bitişten sonra olamaz.";
            return false;
        }

        if (to.DayNumber - from.DayNumber > 92)
        {
            message = "Aralık en fazla 93 gündür.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static TimesheetPersonRow MapPerson(TimesheetPerson person) => new()
    {
        PersonId = person.PersonId,
        Name = person.Name,
        CameDays = person.CameDays,
        LateEntries = person.LateEntries,
        EarlyExits = person.EarlyExits,
        LateExits = person.LateExits,
        BreakCount = person.BreakCount,
        BreakMinutes = person.BreakMinutes,
        WorkMinutes = person.WorkMinutes,
        Open = person.Open,
        Days = person.Days.Select(day => new TimesheetDayRow
        {
            Date = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Absent = day.Absent,
            Open = day.Open,
            Entry = day.Entry,
            EntryClass = day.EntryClass,
            Exit = day.Exit,
            ExitClass = day.ExitClass,
            BreakCount = day.BreakCount,
            BreakMinutes = day.BreakMinutes,
            WorkMinutes = day.WorkMinutes
        }).ToArray()
    };

    private (int Status, string Message)? DenyReport(PdksActor actor)
    {
        var denied = DenyLive(actor);
        if (denied is null)
            return null;
        return (denied.Value.Status, "Bu hesabın raporu görmeye yetkisi yok.");
    }

    private (int Status, string Message)? DenyLive(PdksActor actor)
    {
        var domain = _settings.Value.ServiceAccount.DomainName;
        var sameDomain = !string.IsNullOrWhiteSpace(domain)
            && string.Equals(actor.DomainName, domain, StringComparison.OrdinalIgnoreCase);
        var allowed = actor.CanManage || actor.Groups.Any(group =>
            group.Equals("Yonetim", StringComparison.OrdinalIgnoreCase)
            || group.Equals("idari isler", StringComparison.OrdinalIgnoreCase));
        if (allowed && sameDomain)
            return null;
        return (403, "Bu hesabın anlık durumu görmeye yetkisi yok.");
    }

    private (int Status, string Message)? DenyManager(PdksActor actor)
    {
        var domain = _settings.Value.ServiceAccount.DomainName;
        var sameDomain = !string.IsNullOrWhiteSpace(domain)
            && string.Equals(actor.DomainName, domain, StringComparison.OrdinalIgnoreCase);
        if (actor.CanManage && sameDomain)
            return null;
        return (403, "Bu hesap ekranı bağlayamaz.");
    }

    private string BuildPassUrl(string nonce)
    {
        var root = (_settings.Value.Pdks.PublicAppBaseUrl ?? string.Empty).TrimEnd('/');
        return $"{root}/gecis?n={Uri.EscapeDataString(nonce)}";
    }

    private static string NewNonce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static IssueCodeResponse UndefinedDoor() =>
        new() { StatusCode = 404, Message = "Kapı tanımsız." };

    private static ConsumeCodeResponse Rejected(string? doorKey, string? doorTitle, DateTime? occurredAt, string reason) =>
        new()
        {
            Result = "rejected",
            RejectReason = reason,
            DoorKey = doorKey,
            DoorTitle = doorTitle,
            OccurredAt = occurredAt
        };

    private async Task<bool> DayIsCompleteAsync(string token, string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;
        var rows = await _data.ListAcceptedForPersonAsync(token, userId, 50, cancellationToken);
        return PassageRules.DayIsComplete(rows.Select(row => (row.Direction, row.OccurredAt)), DateTime.UtcNow, _istanbul);
    }

    private async Task<PassagePreviewResponse> CompletedDayAsync(
        string token,
        string nonce,
        string? approvedName,
        PdksActor actor,
        CancellationToken cancellationToken)
    {
        string? doorTitle = null;
        string? locationTitle = null;
        var peeked = await _passcodes.PeekAsync(nonce.Trim(), cancellationToken);
        if (!string.IsNullOrWhiteSpace(peeked.DoorKey))
        {
            var door = await _data.FindDoorAsync(token, peeked.DoorKey, cancellationToken);
            if (door is not null)
            {
                doorTitle = door.Title;
                var places = await _data.ListActiveLocationsAsync(token, cancellationToken);
                var location = places.FirstOrDefault(item => string.Equals(item.Key, door.LocationKey, StringComparison.Ordinal));
                locationTitle = location?.Title ?? door.LocationKey;
            }
        }

        var person = string.IsNullOrWhiteSpace(approvedName) ? actor.FullName : approvedName;
        return new PassagePreviewResponse
        {
            Status = "complete",
            Message = "Bugünkü geçiş tamam.",
            PersonName = string.IsNullOrWhiteSpace(person) ? actor.DisplayName : person,
            DoorTitle = doorTitle,
            LocationTitle = locationTitle
        };
    }

    private async Task<ApprovedPhone> ReadApprovedNameAsync(
        string? publicKey,
        string? bearer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(bearer))
            return new ApprovedPhone(403, "Onaysız anahtar kodu geçişe çevirmez.", null, false);

        try
        {
            var root = _settings.Value.Keeper.BaseUrl.TrimEnd('/');
            var url = $"{root}/api/auth/device?publicKey={Uri.EscapeDataString(publicKey.Trim())}";
            var client = _http.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ApprovedPhone(403, "Onaysız anahtar kodu geçişe çevirmez.", null, false);

            var body = await response.Content.ReadFromJsonAsync<ApprovedDeviceBody>(cancellationToken);
            if (!string.Equals(body?.Status, "valid", StringComparison.Ordinal))
                return new ApprovedPhone(403, "Onaysız anahtar kodu geçişe çevirmez.", null, false);
            return new ApprovedPhone(200, null, body?.PersonName, body?.PhotoRequired == true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Approved phone key could not be read");
            return new ApprovedPhone(503, "Bağlantı yok.", null, false);
        }
    }

    private static bool VerifySignature(string publicKey, string nonce, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;
        try
        {
            var key = Convert.FromBase64String(publicKey.Trim());
            var signed = Convert.FromBase64String(signature.Trim());
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(key, out _);
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(nonce),
                signed,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string HoldMessage(string status) => status switch
    {
        "expired" => "Kodun süresi doldu.",
        "used" => "Bu kod kullanılmış.",
        _ => "Kod tanınmadı."
    };

    private sealed record ApprovedPhone(int StatusCode, string? Message, string? Name, bool PhotoRequired);

    private sealed class ApprovedDeviceBody
    {
        public string? Status { get; set; }
        public string? PersonName { get; set; }
        public bool PhotoRequired { get; set; }
    }
}
