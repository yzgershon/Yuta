using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace Yuta.Services;

public sealed class DarchService : IDisposable
{
    public const string Site = "https://darch-sepia.vercel.app/";
    private const string ClientId = "yuta-windows";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Yuta.Darch.Credentials.v1");
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly bool _disabled;
    private Credentials? _credentials;
    private JsonObject? _snapshot;
    private CancellationTokenSource? _link;
    private string _status = "unlinked", _message = "";
    private long _clockOffset;
    private bool _disposed;
    public event Action? Changed;
    private sealed record Credentials(string AccessToken, string RefreshToken, long ExpiresAt);
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Linked => _credentials != null;
    public JsonObject Status => new() { ["status"] = _status, ["message"] = _message, ["snapshot"] = _snapshot?.DeepClone(), ["clockOffset"] = _clockOffset };
    public (bool Running, long Seconds) FocusClock
    {
        get
        {
            var focus = _snapshot?["focus"]?["session"];
            var running = focus?["status"]?.GetValue<string>() == "running";
            var ends = focus?["endsAt"]?.GetValue<long>() ?? 0;
            return (running && ends > Now + _clockOffset, Math.Max(0, (long)Math.Ceiling((ends - Now - _clockOffset) / 1000d)));
        }
    }
    public DarchService(string root, bool disabled = false) : this(root, disabled, null) { }
    internal DarchService(string root, bool disabled, HttpMessageHandler? transport)
    {
        _http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(Site), Timeout = TimeSpan.FromSeconds(12) };
        _path = Path.Combine(root, "darch-credentials.bin"); _disabled = disabled;
        if (!disabled && File.Exists(_path))
            try
            {
                var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
                _credentials = JsonSerializer.Deserialize<Credentials>(bytes);
                _status = _credentials is null ? "unlinked" : "connected";
            }
            catch { _status = "unlinked"; _message = "Reconnect Darch on this Windows account."; }
    }
    private void Notify() { if (!_disposed) Changed?.Invoke(); }
    private void SaveCredentials(JsonObject value)
    {
        var access = value["access_token"]?.GetValue<string>() ?? throw new InvalidOperationException("Darch did not return an account token.");
        var refresh = value["refresh_token"]?.GetValue<string>() ?? throw new InvalidOperationException("Darch did not return a refresh token.");
        if (access.Length > 300 || refresh.Length > 300) throw new InvalidOperationException("Invalid Darch credentials.");
        var next = new Credentials(access, refresh, Now + (value["expires_in"]?.GetValue<int>() ?? 900) * 1000L);
        var protectedBytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(next), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path + ".tmp", protectedBytes); File.Move(_path + ".tmp", _path, true);
        _credentials = next;
    }
    private async Task<JsonObject> ParseAsync(HttpResponseMessage response)
    {
        await response.Content.LoadIntoBufferAsync(1_500_000);
        var text = await response.Content.ReadAsStringAsync();
        JsonObject? result = null;
        try { result = JsonNode.Parse(text)?.AsObject(); } catch { }
        if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("Darch's integration API is not deployed yet. The Darch update must be merged and deployed first.");
        if (!response.IsSuccessStatusCode)
            throw new DarchException((int)response.StatusCode, result?["error"]?.GetValue<string>() ?? "request_failed", result?["message"]?.GetValue<string>() ?? "Darch could not complete this request. Please try again.");
        return result ?? throw new InvalidOperationException("Darch returned an invalid response.");
    }
    private sealed class DarchException(int status, string code, string message) : Exception(message)
    {
        public int Status { get; } = status;
        public string Code { get; } = code;
    }
    private async Task RefreshTokenAsync()
    {
        var current = _credentials ?? throw new InvalidOperationException("Connect your Darch account first.");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ClientId, ["grant_type"] = "refresh_token", ["refresh_token"] = current.RefreshToken });
        using var response = await _http.PostAsync("api/v1/oauth/token", content);
        try { SaveCredentials(await ParseAsync(response)); }
        catch (DarchException error) when (error.Status is 400 or 401 && error.Code == "invalid_grant")
        {
            _credentials = null; _snapshot = null; _status = "unlinked";
            if (File.Exists(_path)) File.Delete(_path);
            _message = "Your Darch connection expired or was removed. Connect again."; Notify();
            throw new InvalidOperationException(_message);
        }
    }
    private async Task<JsonObject> RequestAsync(HttpMethod method, string path, JsonObject? body = null)
    {
        if (_disabled) throw new InvalidOperationException("Darch network access is disabled in native tests.");
        if (_credentials is null) throw new InvalidOperationException("Connect your Darch account first.");
        if (_credentials.ExpiresAt <= Now + 30000) await RefreshTokenAsync();
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _credentials!.AccessToken);
            if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { await RefreshTokenAsync(); continue; }
            return await ParseAsync(response);
        }
    }
    private void Accept(JsonObject value, bool focusOnly = false)
    {
        if (value["serverTime"] is not JsonValue time || !time.TryGetValue<long>(out _) || value["focus"] is not JsonObject)
            throw new InvalidOperationException("Darch returned an incomplete workspace. Try again.");
        if (focusOnly)
        {
            _snapshot ??= new JsonObject();
            _snapshot["focus"] = value["focus"]?.DeepClone();
            _snapshot["serverTime"] = value["serverTime"]?.DeepClone();
        }
        else _snapshot = value;
        _clockOffset = (value["serverTime"]?.GetValue<long>() ?? Now) - Now;
        _status = "connected"; _message = ""; Notify();
    }
    public async Task<JsonObject> ReadAsync(bool focusOnly = false)
    {
        if (!Linked || _disabled) return Status;
        await _gate.WaitAsync();
        try { if (focusOnly && _snapshot?["tabs"] is null) focusOnly = false; if (Linked) Accept(await RequestAsync(HttpMethod.Get, focusOnly ? "api/v1/focus" : "api/v1/overview"), focusOnly); }
        catch (Exception error) { _status = Linked ? "offline" : "unlinked"; _message = error is TaskCanceledException or HttpRequestException ? "Darch is offline. Showing the last confirmed data." : error.Message; Notify(); }
        finally { _gate.Release(); }
        return Status;
    }
    public async Task<JsonObject> CompleteAssignmentAsync(string id, bool done, long updatedAt)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 300) throw new ArgumentException("Invalid assignment.");
        return await MutateAsync(HttpMethod.Patch, "api/v1/assignments/" + Uri.EscapeDataString(id) + "/completion", new JsonObject { ["done"] = done, ["updatedAt"] = updatedAt });
    }
    public async Task<JsonObject> FocusAsync(JsonObject command)
    {
        var action = command["action"]?.GetValue<string>();
        if (action is not ("start" or "pause" or "resume" or "reset" or "finish")) throw new ArgumentException("Unknown Focus action.");
        var input = new JsonObject { ["action"] = action, ["revision"] = command["revision"]?.DeepClone() ?? 0, ["requestId"] = Guid.NewGuid().ToString("N") };
        foreach (var key in new[] { "mode", "duration", "assignmentId", "activity" }) if (command[key] != null) input[key] = command[key]!.DeepClone();
        return await MutateAsync(HttpMethod.Post, "api/v1/focus", input);
    }
    private async Task<JsonObject> MutateAsync(HttpMethod method, string path, JsonObject input)
    {
        await _gate.WaitAsync();
        try { Accept(await RequestAsync(method, path, input)); return Status; }
        catch (DarchException error) when (error.Status == 409)
        { Accept(await RequestAsync(HttpMethod.Get, "api/v1/overview")); throw new InvalidOperationException(error.Message); }
        finally { _gate.Release(); }
    }
    public async Task<JsonObject> DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _link?.Cancel();
            if (Linked)
            {
                var connections = await RequestAsync(HttpMethod.Get, "api/v1/connections");
                var current = connections["connections"]?.AsArray().FirstOrDefault(x => x?["current"]?.GetValue<bool>() == true);
                if (current != null) await RequestAsync(HttpMethod.Delete, "api/v1/connections/" + current["id"]!.GetValue<string>());
            }
            _credentials = null; _snapshot = null; _status = "unlinked"; _message = "";
            if (File.Exists(_path)) File.Delete(_path); Notify(); return Status;
        }
        finally { _gate.Release(); }
    }
    public JsonObject BeginConnect()
    {
        if (_disabled) throw new InvalidOperationException("Account linking is disabled in tests.");
        if (_link != null || Linked) return Status;
        _link = new CancellationTokenSource(TimeSpan.FromMinutes(5)); _status = "connecting"; _message = "Approve the connection in Chrome.";
        _ = LinkAsync(_link.Token); Notify(); return Status;
    }
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private async Task LinkAsync(CancellationToken cancellation)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            using (var availability = await _http.GetAsync("api/v1/capabilities", cancellation))
                if (availability.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Unauthorized)) await ParseAsync(availability);
            listener.Start(4);
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var redirect = $"http://127.0.0.1:{port}/darch/callback/";
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32)); var state = Base64Url(RandomNumberGenerator.GetBytes(32));
            using var query = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ClientId, ["response_type"] = "code", ["redirect_uri"] = redirect, ["state"] = state, ["scope"] = "planner:read assignments:write focus:write workspace:write social:write practice:write account:manage admin:write", ["code_challenge_method"] = "S256", ["code_challenge"] = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier))) });
            OpenChrome(Site + "connect/yuta?" + await query.ReadAsStringAsync(cancellation));
            while (!cancellation.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellation);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var stream = client.GetStream();
                string? line; try { line = await ReadRequestLineAsync(stream, timeout.Token); } catch (OperationCanceledException) { continue; }
                if (line is null || line.Length > 4096 || !line.StartsWith("GET /darch/callback/?", StringComparison.Ordinal)) { await CallbackReply(stream, false); continue; }
                var target = line.Split(' ')[1]; var callback = new Uri(new Uri(redirect), target);
                var parameters = callback.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).GroupBy(x => x[0]).ToDictionary(x => x.Key, x => Uri.UnescapeDataString(x.First()[1]));
                if (parameters.GetValueOrDefault("state") != state) { await CallbackReply(stream, false); continue; }
                if (parameters.GetValueOrDefault("error") == "access_denied") { await CallbackReply(stream, false); throw new InvalidOperationException("Darch connection cancelled."); }
                var code = parameters.GetValueOrDefault("code");
                if (string.IsNullOrWhiteSpace(code) || code.Length > 160) { await CallbackReply(stream, false); continue; }
                await _gate.WaitAsync(cancellation);
                try
                {
                    using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ClientId, ["grant_type"] = "authorization_code", ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect });
                    using var response = await _http.PostAsync("api/v1/oauth/token", content, cancellation);
                    SaveCredentials(await ParseAsync(response)); _status = "connected"; _message = "";
                }
                finally { _gate.Release(); }
                await CallbackReply(stream, true); await ReadAsync(); return;
            }
        }
        catch (Exception error) { if (!_disposed) { _status = Linked ? "connected" : "unlinked"; _message = error is OperationCanceledException ? "The connection timed out. Try again." : error.Message; } }
        finally { listener.Stop(); _link?.Dispose(); _link = null; Notify(); }
    }
    // Bound input before allocating a line; an unrelated local process cannot grow it indefinitely.
    internal static async Task<string?> ReadRequestLineAsync(Stream stream, CancellationToken cancellation)
    {
        var buffer = new byte[4096]; var one = new byte[1]; var count = 0;
        while (count < buffer.Length)
        {
            if (await stream.ReadAsync(one, cancellation) == 0) return null;
            if (one[0] == (byte)'\n') return Encoding.ASCII.GetString(buffer, 0, count).TrimEnd('\r');
            buffer[count++] = one[0];
        }
        return null;
    }
    public async Task<JsonObject> FrameAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var result = await RequestAsync(HttpMethod.Post, "api/v1/embed-ticket", new JsonObject());
            var ticket = result["ticket"]?.GetValue<string>() ?? throw new InvalidOperationException("Darch did not return a workspace ticket.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(ticket, @"^dt\.[A-Za-z0-9_-]{32}\.[A-Za-z0-9_-]{43}$")) throw new InvalidOperationException("Invalid workspace ticket.");
            return new JsonObject { ["url"] = Site + "yuta#" + ticket };
        }
        finally { _gate.Release(); }
    }
    private static async Task CallbackReply(Stream stream, bool success)
    {
        var body = Encoding.UTF8.GetBytes("<!doctype html><html><head><meta charset=\"utf-8\"><title>Darch + Yuta</title></head><body><h1>" + (success ? "Darch is connected to Yuta." : "Connection not completed.") + "</h1><p>You can return to Yuta and close this tab.</p></body></html>");
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(success ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nContent-Security-Policy: default-src 'none'; frame-ancestors 'none'\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
        try { await stream.WriteAsync(header); await stream.WriteAsync(body); await stream.FlushAsync(); } catch (IOException) { }
    }
    public static void OpenSection(string section)
    {
        if (section is not ("today" or "assignments" or "calendar" or "focus" or "timelog" or "grades" or "games" or "sat" or "settings" or "admin")) section = "today";
        OpenChrome(Site + "#" + section);
    }
    private static void OpenChrome(string url)
    {
        string? chrome = null;
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        { using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"); var candidate = (key?.GetValue(null) as string)?.Trim('"'); if (candidate != null && File.Exists(candidate) && Path.GetFileName(candidate).Equals("chrome.exe", StringComparison.OrdinalIgnoreCase)) { chrome = candidate; break; } }
        if (chrome == null) throw new InvalidOperationException("Install Google Chrome to open Darch from Yuta.");
        var start = new ProcessStartInfo(chrome) { UseShellExecute = true }; start.ArgumentList.Add(url); Process.Start(start);
    }
    public void Dispose() { _disposed = true; _link?.Cancel(); _http.Dispose(); }
}
