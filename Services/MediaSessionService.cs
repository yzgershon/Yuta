using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Yuta.Services;

public sealed record MediaSnapshot
{
    public bool Available { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string Source { get; init; } = "";
    public string SourceName { get; init; } = "";
    public string? Artwork { get; init; }
    public bool Playing { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public long UpdatedAt { get; init; }
    public double PlaybackRate { get; init; } = 1;
    public bool CanPlay { get; init; }
    public bool CanPause { get; init; }
    public bool CanToggle { get; init; }
    public bool CanNext { get; init; }
    public bool CanPrevious { get; init; }
    public bool CanSeek { get; init; }
    public long Revision { get; init; }
}

// Windows owns the session and transport commands. This service only observes it;
// metadata/artwork never share a queue with play/pause and never run on the UI thread.
public sealed class MediaSessionService : IDisposable
{
    private readonly object _gate = new();
    private readonly object _refreshGate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<GlobalSystemMediaTransportControlsSession> _subscriptions = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaSnapshot _snapshot = new();
    private Task? _initialization;
    private CancellationTokenSource? _metadataCancellation;
    private long _metadataVersion;
    private int _pending;
    private bool _refreshing, _disposed;
    private const int Sessions = 1, Playback = 2, Metadata = 4;
    public event Action<MediaSnapshot>? Changed;

    public Task InitializeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            if (_initialization is { IsCompleted: true } && _manager is null) _initialization = null;
            return _initialization ??= Task.Run(async () =>
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(timeout.Token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_disposed) return;
                        _manager = manager;
                        manager.SessionsChanged += OnSessionsChanged;
                        manager.CurrentSessionChanged += OnCurrentChanged;
                    }
                    Refresh(Sessions | Metadata);
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { Program.Log(error); }
            });
        }
    }

    public async Task<MediaSnapshot> ReadAsync()
    {
        await InitializeAsync().ConfigureAwait(false);
        lock (_gate) return _snapshot;
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => Queue(Sessions);
    private void OnCurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => Queue(Sessions);
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => Queue(Sessions | Playback);
    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => Queue(Playback);
    private void OnMetadataChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => Queue(Metadata | Playback);

    private void Queue(int changes)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending |= changes;
            if (_refreshing) return;
            _refreshing = true;
        }
        _ = Task.Run(() =>
        {
            while (true)
            {
                int work;
                lock (_gate)
                {
                    if (_disposed || _pending == 0) { _refreshing = false; return; }
                    work = _pending; _pending = 0;
                }
                Refresh(work);
            }
        });
    }

    private void Refresh(int changes)
    {
        // Initialization, event delivery, and accepted transport commands can all
        // request a read. Serialize only these short worker reads so an older
        // playback snapshot cannot be published with a newer revision.
        lock (_refreshGate) RefreshCore(changes);
    }
    private void RefreshCore(int changes)
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? selected;
            bool changed = false;
            lock (_gate)
            {
                if (_disposed || _manager is null) return;
                if ((changes & Sessions) != 0)
                {
                    var sessions = _manager.GetSessions().ToArray();
                    foreach (var removed in _subscriptions.Where(x => !sessions.Contains(x)).ToArray())
                    {
                        removed.PlaybackInfoChanged -= OnPlaybackChanged;
                        _subscriptions.Remove(removed);
                    }
                    foreach (var added in sessions.Where(x => !_subscriptions.Contains(x)))
                    {
                        added.PlaybackInfoChanged += OnPlaybackChanged;
                        _subscriptions.Add(added);
                    }
                    var current = _manager.GetCurrentSession();
                    int currentIndex = Array.IndexOf(sessions, current);
                    int selectedIndex = SelectSession(sessions.Select(IsPlaying).ToArray(), currentIndex, Array.IndexOf(sessions, _session));
                    selected = selectedIndex >= 0 ? sessions[selectedIndex] : null;
                    changed = !Equals(selected, _session);
                    if (changed)
                    {
                        if (_session is not null)
                        {
                            _session.MediaPropertiesChanged -= OnMetadataChanged;
                            _session.TimelinePropertiesChanged -= OnTimelineChanged;
                        }
                        _metadataCancellation?.Cancel();
                        _metadataVersion++;
                        _session = selected;
                        _snapshot = new MediaSnapshot { Revision = _snapshot.Revision };
                        if (selected is not null)
                        {
                            selected.MediaPropertiesChanged += OnMetadataChanged;
                            selected.TimelinePropertiesChanged += OnTimelineChanged;
                        }
                    }
                }
                selected = _session;
            }
            if (selected is null) { Publish(_ => new MediaSnapshot(), null); return; }
            var playback = ReadPlayback(selected, new MediaSnapshot());
            Publish(current => playback with { Title = current.Title, Artist = current.Artist, Album = current.Album, Artwork = current.Artwork }, selected);
            if (changed || (changes & Metadata) != 0) StartMetadata(selected);
        }
        catch (Exception error) { if (!_disposed) Program.Log(error); }
    }

    internal static int SelectSession(IReadOnlyList<bool> playing, int current, int previous)
    {
        if (current >= 0 && current < playing.Count && playing[current]) return current;
        for (var i = 0; i < playing.Count; i++) if (playing[i]) return i;
        if (current >= 0 && current < playing.Count) return current;
        if (previous >= 0 && previous < playing.Count) return previous;
        return playing.Count > 0 ? 0 : -1;
    }
    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try { return session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
        catch { return false; }
    }
    private static MediaSnapshot ReadPlayback(GlobalSystemMediaTransportControlsSession session, MediaSnapshot snapshot)
    {
        var playback = session.GetPlaybackInfo();
        // A disappearing browser/player session can briefly return no WinRT info.
        if (playback is null) return new MediaSnapshot { Revision = snapshot.Revision };
        var controls = playback.Controls;
        var timeline = session.GetTimelineProperties();
        var now = DateTimeOffset.UtcNow;
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var rate = playback.PlaybackRate ?? 1;
        if (!double.IsFinite(rate) || rate < 0) rate = 1;
        var duration = timeline is null ? 0 : Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
        var position = timeline is null ? 0 : ProjectPosition((timeline.Position - timeline.StartTime).TotalSeconds, duration,
            timeline.LastUpdatedTime, now, playing, rate);
        var source = session.SourceAppUserModelId ?? "";
        return snapshot with
        {
            Available = true, Source = source, SourceName = FriendlySource(source), Playing = playing,
            Position = position, Duration = duration, UpdatedAt = now.ToUnixTimeMilliseconds(), PlaybackRate = rate,
            CanPlay = controls?.IsPlayEnabled == true, CanPause = controls?.IsPauseEnabled == true,
            CanToggle = controls?.IsPlayPauseToggleEnabled == true || (playing ? controls?.IsPauseEnabled == true : controls?.IsPlayEnabled == true),
            CanNext = controls?.IsNextEnabled == true, CanPrevious = controls?.IsPreviousEnabled == true,
            CanSeek = controls?.IsPlaybackPositionEnabled == true && duration > 0
        };
    }
    internal static double ProjectPosition(double position, double duration, DateTimeOffset updated, DateTimeOffset now, bool playing, double rate)
    {
        // Some players publish a zero/invalid timestamp. Do not invent elapsed time in that case.
        var elapsed = (now - updated).TotalSeconds;
        if (playing && updated > DateTimeOffset.UnixEpoch && elapsed is >= 0 and < 86400 && double.IsFinite(rate)) position += elapsed * rate;
        return Math.Clamp(double.IsFinite(position) ? position : 0, 0, duration > 0 && double.IsFinite(duration) ? duration : double.MaxValue);
    }
    internal static string FriendlySource(string source)
    {
        if (source.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (source.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (source.Contains("msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        if (source.Contains("firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (source.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase)) return "Apple Music";
        if (source.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase)) return "Media Player";
        return Path.GetFileNameWithoutExtension(source.Split('!')[0]);
    }

    private void StartMetadata(GlobalSystemMediaTransportControlsSession session)
    {
        long version;
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_disposed || !Equals(session, _session)) return;
            _metadataCancellation?.Cancel();
            _metadataCancellation = cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            cancellation.CancelAfter(TimeSpan.FromSeconds(5));
            version = ++_metadataVersion;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var props = await session.TryGetMediaPropertiesAsync().AsTask(cancellation.Token).ConfigureAwait(false);
                // Merge only metadata: a playback/timeline event may have arrived while
                // the player was returning its properties. Never revive its older state.
                Publish(current => current with { Title = props.Title ?? "", Artist = props.Artist ?? "", Album = props.AlbumTitle ?? "", Artwork = null }, session, version);
                var artwork = await ReadArtworkAsync(props.Thumbnail, cancellation.Token).ConfigureAwait(false);
                Publish(current => current with { Artwork = artwork }, session, version);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!_disposed) Program.Log(error); }
            finally
            {
                lock (_gate) if (ReferenceEquals(_metadataCancellation, cancellation)) _metadataCancellation = null;
                cancellation.Dispose();
            }
        });
    }

    private static async Task<string?> ReadArtworkAsync(IRandomAccessStreamReference? reference, CancellationToken token)
    {
        if (reference is null) return null;
        using var stream = await reference.OpenReadAsync().AsTask(token).ConfigureAwait(false);
        if (stream.Size == 0 || stream.Size > 8 * 1024 * 1024) return null;
        using var input = stream.GetInputStreamAt(0);
        using var reader = new DataReader(input);
        var length = (uint)stream.Size;
        if (await reader.LoadAsync(length).AsTask(token).ConfigureAwait(false) != length) return null;
        var bytes = new byte[length]; reader.ReadBytes(bytes);
        token.ThrowIfCancellationRequested();
        using var originalStream = new MemoryStream(bytes, false);
        using var original = Image.FromStream(originalStream);
        if (original.Width > 8192 || original.Height > 8192) return null;
        var scale = Math.Min(1d, 256d / Math.Max(original.Width, original.Height));
        using var thumb = new Bitmap(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
        using (var graphics = Graphics.FromImage(thumb))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original, 0, 0, thumb.Width, thumb.Height);
        }
        // A bounded JPEG keeps frequent bridge updates small even when the source
        // supplies a large lossless album cover. The UI never decodes the original.
        using var result = new MemoryStream(); thumb.Save(result, ImageFormat.Jpeg);
        return "data:image/jpeg;base64," + Convert.ToBase64String(result.ToArray());
    }

    private void Publish(Func<MediaSnapshot, MediaSnapshot> update, GlobalSystemMediaTransportControlsSession? session, long? metadataVersion = null)
    {
        MediaSnapshot value;
        Action<MediaSnapshot>? changed;
        lock (_gate)
        {
            if (_disposed || !Equals(session, _session) || (metadataVersion.HasValue && metadataVersion != _metadataVersion)) return;
            _snapshot = update(_snapshot) with { Revision = _snapshot.Revision + 1 };
            value = _snapshot; changed = Changed;
        }
        // Consumers marshal to their UI thread. A closed window must not kill the event worker.
        try { changed?.Invoke(value); } catch (Exception error) { if (!_disposed) Program.Log(error); }
    }

    public async Task<bool> ActionAsync(string action, double? position = null, string? source = null, string? trackTitle = null)
    {
        await InitializeAsync().ConfigureAwait(false);
        GlobalSystemMediaTransportControlsSession session;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            session = _session ?? throw new InvalidOperationException("Start playing something in a music or video app first.");
            if (!string.IsNullOrEmpty(source) && !string.Equals(source, session.SourceAppUserModelId, StringComparison.Ordinal))
                throw new InvalidOperationException("The active music app changed. Try the control again.");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            // Commands use the already selected session directly. They do not wait for artwork,
            // refresh volume/WMI, or fall back to a global key that could target another player.
            var success = await Task.Run(async () =>
            {
                lock (_gate)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (action == "seek" && (!Equals(session, _session) || trackTitle is not null && !string.Equals(trackTitle, _snapshot.Title, StringComparison.Ordinal)))
                        throw new InvalidOperationException("The track changed. Try seeking again.");
                }
                return action switch
                {
                    "play" => await SetPlayingAsync(session, true, timeout.Token),
                    "pause" => await SetPlayingAsync(session, false, timeout.Token),
                    "toggle" => await session.TryTogglePlayPauseAsync().AsTask(timeout.Token),
                    "next" => await session.TrySkipNextAsync().AsTask(timeout.Token),
                    "previous" => await session.TrySkipPreviousAsync().AsTask(timeout.Token),
                    "seek" when position.HasValue && double.IsFinite(position.Value) => await session.TryChangePlaybackPositionAsync(SeekTicks(session, position.Value)).AsTask(timeout.Token),
                    _ => throw new ArgumentException("Unsupported playback action.")
                };
            }, timeout.Token).ConfigureAwait(false);
            if (!success) throw new InvalidOperationException("The music app could not perform that playback action. Try its own controls.");
            // Refresh the cheap transport/timeline state before answering the bridge.
            // Metadata and thumbnail retrieval remain independent.
            await Task.Run(() => Refresh(Playback), _lifetime.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!_disposed) { throw new InvalidOperationException("The music app did not respond. Try again."); }
    }
    private static async Task<bool> SetPlayingAsync(GlobalSystemMediaTransportControlsSession session, bool playing, CancellationToken token)
    {
        var playback = session.GetPlaybackInfo();
        if ((playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) == playing) return true;
        if (playing && playback.Controls.IsPlayEnabled) return await session.TryPlayAsync().AsTask(token);
        if (!playing && playback.Controls.IsPauseEnabled) return await session.TryPauseAsync().AsTask(token);
        return playback.Controls.IsPlayPauseToggleEnabled && await session.TryTogglePlayPauseAsync().AsTask(token);
    }
    private static long SeekTicks(GlobalSystemMediaTransportControlsSession session, double seconds)
    {
        var timeline = session.GetTimelineProperties();
        var value = timeline.StartTime.TotalSeconds + Math.Clamp(seconds, 0, Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds));
        return TimeSpan.FromSeconds(value).Ticks;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _lifetime.Cancel(); _metadataCancellation?.Cancel();
            if (_manager is not null)
            {
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager.CurrentSessionChanged -= OnCurrentChanged;
            }
            foreach (var session in _subscriptions) session.PlaybackInfoChanged -= OnPlaybackChanged;
            _subscriptions.Clear();
            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnMetadataChanged;
                _session.TimelinePropertiesChanged -= OnTimelineChanged;
            }
            _session = null; _manager = null; Changed = null;
            _lifetime.Dispose();
        }
    }
}
