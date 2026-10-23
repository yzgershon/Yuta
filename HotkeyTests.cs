using System.Text.Json.Nodes;
using Yuta.Services;

namespace Yuta;

internal static class HotkeyTests
{
    internal static void Run(Action<string, Func<bool>> check, string root)
    {
        check("Shortcut parser maps modifiers and supported keys consistently", () =>
        {
            var value = Hotkey.Parse("shift + alt + ctrl + k");
            return value.Shortcut == "Ctrl+Alt+Shift+K" && value.Modifiers == 7 && value.Key == 0x4B
                && Hotkey.Parse("Alt+Space").Key == 0x20 && Hotkey.Parse("Win+F24").Key == 0x87
                && Hotkey.FromVirtualKey(0x20, 1).Shortcut == "Alt+Space" && Hotkey.FromVirtualKey(0x21, 6).Shortcut == "Ctrl+Shift+PageUp";
        });
        check("Shortcut validation preserves ordinary typing and rejects incomplete chords", () =>
        {
            foreach (var value in new[] { "", "K", "Shift+K", "Ctrl", "Alt+Escape", "Ctrl+Ctrl+K", "Alt+F25" })
            {
                try { Hotkey.Parse(value); return false; } catch (ArgumentException) { }
            }
            return true;
        });
        check("A conflicting shortcut keeps the prior registration and persisted choice", () =>
        {
            var active = new HashSet<int>(); var persisted = Hotkey.Default;
            using var binding = new HotkeyBinding((id, modifiers, key) => { if (key == 0x4B) return false; active.Add(id); return (modifiers & 0x4000) != 0; }, id => active.Remove(id));
            if (!binding.TrySet(Hotkey.Default)) return false;
            var previousId = binding.Id;
            return !binding.TrySet("Ctrl+Shift+K", key => persisted = key.Shortcut)
                && binding.Id == previousId && binding.Current.Shortcut == Hotkey.Default && persisted == Hotkey.Default
                && active.SetEquals(new[] { previousId });
        });
        check("A new shortcut is acquired and saved before releasing the old shortcut", () =>
        {
            var events = new List<string>();
            using var binding = new HotkeyBinding((id, modifiers, key) => { events.Add("register:" + id); return true; }, id => events.Add("release:" + id));
            binding.TrySet(Hotkey.Default); events.Clear();
            if (!binding.TrySet("Ctrl+Shift+Y", _ => events.Add("save"))) return false;
            var passed = events.SequenceEqual(new[] { "register:2", "save", "release:1" }) && binding.Id == 2 && binding.Current.Shortcut == "Ctrl+Shift+Y";
            binding.Dispose();
            return passed && events[^1] == "release:2" && !binding.Registered;
        });
        check("A settings write failure releases the candidate and keeps the active shortcut", () =>
        {
            var active = new HashSet<int>();
            using var binding = new HotkeyBinding((id, modifiers, key) => active.Add(id), id => active.Remove(id));
            binding.TrySet(Hotkey.Default);
            try { binding.TrySet("Ctrl+Shift+Y", _ => throw new IOException("Fixture save failure")); return false; }
            catch (IOException) { return binding.Current.Shortcut == Hotkey.Default && binding.Id == 1 && active.SetEquals(new[] { 1 }); }
        });
        check("The old shortcut migrates once without losing saved work or later choices", () =>
        {
            var directory = Path.Combine(root, "hotkey-profile"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "state.json"), new JsonObject
            {
                ["notes"] = "Keep this note", ["todos"] = new JsonArray(new JsonObject { ["text"] = "Keep this task" }),
                ["settings"] = new JsonObject { ["hotkey"] = "Ctrl+Alt+Space", ["accent"] = "rose" }
            }.ToJsonString());
            var store = new Store(directory);
            if (store.Data["settings"]!["hotkey"]!.GetValue<string>() != Hotkey.Default || store.Data["hotkeyVersion"]!.GetValue<int>() != 1) return false;
            if (store.Data["settings"]!["accent"]!.GetValue<string>() != "custom" || store.Data["settings"]!["customAccent"]!.GetValue<string>() != "#9b80ff") return false;
            var settings = store.Data["settings"]!.DeepClone(); settings["hotkey"] = "Ctrl+Alt+Space";
            settings["accent"] = "rose";
            store.Update("settings", settings);
            var reopened = new Store(directory);
            return reopened.Data["settings"]!["hotkey"]!.GetValue<string>() == "Ctrl+Alt+Space"
                && reopened.Data["notes"]!.GetValue<string>() == "Keep this note"
                && reopened.Data["todos"]![0]!["text"]!.GetValue<string>() == "Keep this task"
                && reopened.Data["settings"]!["accent"]!.GetValue<string>() == "rose";
        });
        check("Fresh profiles use Alt Space and existing custom shortcuts survive migration", () =>
        {
            var directory = Path.Combine(root, "hotkey-custom");
            var fresh = new Store(directory);
            if (fresh.Data["settings"]!["hotkey"]!.GetValue<string>() != Hotkey.Default) return false;
            fresh.Data.Remove("hotkeyVersion"); fresh.Data["settings"]!["hotkey"] = "Ctrl+Shift+Y"; fresh.Save();
            return new Store(directory).Data["settings"]!["hotkey"]!.GetValue<string>() == "Ctrl+Shift+Y";
        });
    }
}
