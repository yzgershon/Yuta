using System.Text.Json;
using System.Text.Json.Nodes;

namespace Yuta.Services;

public sealed class Store
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
    public string Root { get; }
    public JsonObject Data { get; private set; }
    public Store(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Perch");
        Directory.CreateDirectory(Root);
        Data = new JsonObject();
        var path = Path.Combine(Root, "state.json");
        if (File.Exists(path))
        {
            try { Data = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new(); }
            catch { File.Copy(path, path + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); }
        }
        foreach (var key in new[] { "todos", "events", "routines", "snippets", "favorites", "recent", "files" }) Data[key] ??= new JsonArray();
        Data["notes"] ??= "";
        Data["settings"] ??= new JsonObject { ["shape"] = "notch", ["monitor"] = 0, ["clipboardEnabled"] = false, ["city"] = "", ["fahrenheit"] = true, ["openOnHover"] = true, ["accent"] = "custom", ["customAccent"] = AccentPalette.DefaultCustom };
        var accent = Data["settings"]!["accent"] is JsonValue savedAccent && savedAccent.TryGetValue<string>(out var name) ? name : null;
        var needsSave = accent != AccentPalette.Normalize(accent);
        if (needsSave) Data["settings"]!["accent"] = "lavender";
        var savedCustom = Data["settings"]!["customAccent"] is JsonValue customValue && customValue.TryGetValue<string>(out var customColor) ? customColor : null;
        var custom = AccentPalette.NormalizeCustom(savedCustom);
        if (savedCustom != custom) { Data["settings"]!["customAccent"] = custom; needsSave = true; }
        // Keep the existing profile and encrypted clipboard in place. Apply the
        // requested notch preference once; subsequent user choices remain intact.
        if (Data["yutaInterfaceVersion"] is not JsonValue version || !version.TryGetValue<int>(out var current) || current < 1)
        {
            Data["settings"]!["shape"] = "notch";
            Data["settings"]!["openOnHover"] = true;
            Data["yutaInterfaceVersion"] = 1;
            needsSave = true;
        }
        // Apply the user's exported workbench appearance once. Later choices
        // in Settings survive reopening; other profile data is untouched.
        if (Data["workbenchDesignVersion"] is not JsonValue designVersion || !designVersion.TryGetValue<int>(out var designCurrent) || designCurrent < 1)
        {
            Data["settings"]!["accent"] = "custom";
            Data["settings"]!["customAccent"] = AccentPalette.DefaultCustom;
            Data["settings"]!["shape"] = "notch";
            Data["settings"]!["openOnHover"] = true;
            Data["workbenchDesignVersion"] = 1;
            needsSave = true;
        }
        var savedHotkey = Data["settings"]!["hotkey"] is JsonValue hotkeyValue && hotkeyValue.TryGetValue<string>(out var shortcut) ? shortcut : null;
        var normalizedHotkey = Hotkey.Normalize(savedHotkey);
        if (Data["hotkeyVersion"] is not JsonValue hotkeyVersion || !hotkeyVersion.TryGetValue<int>(out var hotkeyCurrent) || hotkeyCurrent < 1)
        {
            if (normalizedHotkey == "Ctrl+Alt+Space") normalizedHotkey = Hotkey.Default;
            Data["hotkeyVersion"] = 1;
            needsSave = true;
        }
        if (savedHotkey != normalizedHotkey) { Data["settings"]!["hotkey"] = normalizedHotkey; needsSave = true; }
        if (needsSave) Save();
    }
    public void Save()
    {
        var path = Path.Combine(Root, "state.json");
        File.WriteAllText(path + ".tmp", Data.ToJsonString(JsonOptions));
        File.Move(path + ".tmp", path, true);
    }
    public object Update(string key, JsonNode? value)
    {
        if (!new[] { "todos", "events", "routines", "snippets", "favorites", "recent", "files", "notes", "settings", "timer", "visibility" }.Contains(key)) throw new ArgumentException("Unknown setting.");
        if ((value?.ToJsonString().Length ?? 0) > 1_000_000) throw new ArgumentException("This entry is too large.");
        if (key == "notes" && value is not JsonValue) throw new ArgumentException("Notes must be text.");
        if (key is "settings" or "timer" or "visibility" && value is not JsonObject) throw new ArgumentException("Invalid settings.");
        if (key is not ("notes" or "settings" or "timer" or "visibility") && value is not JsonArray) throw new ArgumentException("A list is required.");
        var next = value?.DeepClone();
        if (key == "settings")
        {
            next!["hotkey"] = Hotkey.Parse(next["hotkey"]?.GetValue<string>() ?? Data["settings"]?["hotkey"]?.GetValue<string>() ?? Hotkey.Default).Shortcut;
            next["accent"] = AccentPalette.Normalize(next["accent"]?.GetValue<string>());
            next["customAccent"] = AccentPalette.NormalizeCustom(next["customAccent"]?.GetValue<string>());
        }
        var previous = Data[key]?.DeepClone();
        Data[key] = next;
        try { Save(); } catch { Data[key] = previous; throw; }
        return true;
    }
}
