using Yuta.Services;

namespace Yuta;

internal static class MediaSessionTests
{
    internal static void Run(Action<string, Func<bool>> check)
    {
        check("Media selection follows playing audio instead of a stale paused browser", () => MediaSessionService.SelectSession(new[] { false, true }, 0, 0) == 1);
        check("Media selection respects Windows current player when two players are active", () => MediaSessionService.SelectSession(new[] { true, true }, 1, 0) == 1);
        check("Paused current media remains controllable and no sessions are handled", () => MediaSessionService.SelectSession(new[] { false, false }, 1, 0) == 1 && MediaSessionService.SelectSession(Array.Empty<bool>(), -1, -1) == -1);
        check("Timeline advances from the Windows timestamp and clamps at track end", () =>
        {
            var now = DateTimeOffset.UtcNow;
            return MediaSessionService.ProjectPosition(20, 100, now.AddSeconds(-5), now, true, 1) == 25
                && MediaSessionService.ProjectPosition(99, 100, now.AddSeconds(-5), now, true, 1) == 100;
        });
        check("Paused, invalid and future media timestamps do not invent progress", () =>
        {
            var now = DateTimeOffset.UtcNow;
            return MediaSessionService.ProjectPosition(20, 100, now.AddSeconds(-5), now, false, 1) == 20
                && MediaSessionService.ProjectPosition(20, 100, DateTimeOffset.MinValue, now, true, 1) == 20
                && MediaSessionService.ProjectPosition(20, 100, now.AddSeconds(5), now, true, 1) == 20;
        });
        check("Spotify desktop source identifiers have a useful player label", () => MediaSessionService.FriendlySource("Spotify.exe") == "Spotify" && MediaSessionService.FriendlySource("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify") == "Spotify");
    }
}
