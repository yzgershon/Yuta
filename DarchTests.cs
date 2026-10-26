using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yuta.Services;

namespace Yuta;
internal static class DarchTests
{
    private sealed class Transport(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation) => respond(request);
    }
    private static HttpResponseMessage Json(string value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static string Seed(string root)
    {
        var dir = Path.Combine(root, "darch-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var data = JsonSerializer.SerializeToUtf8Bytes(new { AccessToken = "fixture-old-access", RefreshToken = "fixture-old-refresh", ExpiresAt = 1L });
        File.WriteAllBytes(Path.Combine(dir, "darch-credentials.bin"), ProtectedData.Protect(data, Encoding.UTF8.GetBytes("Yuta.Darch.Credentials.v1"), DataProtectionScope.CurrentUser));
        return dir;
    }
    internal static void Run(Action<string, Func<bool>> check, string root)
    {
        check("Darch rotates protected credentials once and exposes only workspace data", () =>
        {
            var dir = Seed(root); var refreshes = 0; var authorized = true;
            using var service = new DarchService(dir, false, new Transport(async request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/token"))
                {
                    refreshes++; authorized &= request.Headers.Authorization is null && (await request.Content!.ReadAsStringAsync()).Contains("fixture-old-refresh");
                    return Json("{\"access_token\":\"fixture-new-access\",\"refresh_token\":\"fixture-new-refresh\",\"expires_in\":900}");
                }
                authorized &= request.Headers.Authorization?.Parameter == "fixture-new-access";
                return Json(new JsonObject { ["serverTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ["tabs"] = new JsonArray(), ["account"] = new JsonObject { ["displayName"] = "Fixture" }, ["focus"] = new JsonObject { ["enabled"] = true } }.ToJsonString());
            }));
            service.ReadAsync(true).GetAwaiter().GetResult(); service.ReadAsync().GetAwaiter().GetResult();
            var protectedText = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(dir, "darch-credentials.bin")));
            return refreshes == 1 && authorized && service.Linked && !service.Status.ToJsonString().Contains("fixture-new") && !protectedText.Contains("fixture-new") && service.Status["snapshot"]?["account"]?["displayName"]?.GetValue<string>() == "Fixture";
        });
        check("Darch revoked refresh clears local credentials and old account data", () =>
        {
            var dir = Seed(root);
            using var service = new DarchService(dir, false, new Transport(_ => Task.FromResult(Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest))));
            service.ReadAsync().GetAwaiter().GetResult();
            return !service.Linked && service.Status["status"]?.GetValue<string>() == "unlinked" && service.Status["snapshot"] is null && !File.Exists(Path.Combine(dir, "darch-credentials.bin"));
        });
        check("Darch OAuth loopback input is bounded before allocating an untrusted line", () =>
        {
            using var oversized = new MemoryStream(new byte[5000]);
            var rejected = DarchService.ReadRequestLineAsync(oversized, CancellationToken.None).GetAwaiter().GetResult();
            using var valid = new MemoryStream(Encoding.ASCII.GetBytes("GET /darch/callback/?state=fixture HTTP/1.1\r\n"));
            return rejected is null && oversized.Position == 4096 && DarchService.ReadRequestLineAsync(valid, CancellationToken.None).GetAwaiter().GetResult() == "GET /darch/callback/?state=fixture HTTP/1.1";
        });
        check("Darch native tests cannot link or touch the network", () =>
        {
            using var service = new DarchService(Seed(root), true, new Transport(_ => throw new InvalidOperationException("Network must not run")));
            var status = service.ReadAsync().GetAwaiter().GetResult();
            try { service.BeginConnect(); return false; } catch (InvalidOperationException) { return !service.Linked && status["status"]?.GetValue<string>() == "unlinked"; }
        });
    }
}
