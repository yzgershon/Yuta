using Yuta.Services;
using System.Runtime.InteropServices;

namespace Yuta;

internal readonly record struct Hotkey(string Shortcut, uint Modifiers, uint Key)
{
    internal const string Default = "Alt+Space";
    internal string Label => Shortcut.Replace("+", " + ");
    internal static Hotkey Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 80) throw new ArgumentException("Choose a shortcut with Ctrl, Alt, or Win and a key.");
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        uint modifiers = 0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var flag = parts[i].ToLowerInvariant() switch { "ctrl" => 2u, "alt" => 1u, "shift" => 4u, "win" => 8u, _ => 0u };
            if (flag == 0 || (modifiers & flag) != 0) throw new ArgumentException("Use each modifier once: Ctrl, Alt, Shift, or Win.");
            modifiers |= flag;
        }
        if ((modifiers & 11) == 0) throw new ArgumentException("Include Ctrl, Alt, or Win so normal typing stays available.");
        var raw = parts[^1];
        var upper = raw.ToUpperInvariant();
        uint key; string name;
        if (upper.Length == 1 && char.IsAsciiLetterOrDigit(upper[0])) { name = upper; key = upper[0]; }
        else if (upper.StartsWith('F') && int.TryParse(upper.AsSpan(1), out var f) && f is >= 1 and <= 24) { name = "F" + f; key = (uint)(0x70 + f - 1); }
        else
        {
            (name, key) = upper switch
            {
                "SPACE" => ("Space", 0x20u), "TAB" => ("Tab", 0x09u), "ENTER" => ("Enter", 0x0Du),
                "BACKSPACE" => ("Backspace", 0x08u), "DELETE" => ("Delete", 0x2Eu), "INSERT" => ("Insert", 0x2Du),
                "HOME" => ("Home", 0x24u), "END" => ("End", 0x23u), "PAGEUP" => ("PageUp", 0x21u), "PAGEDOWN" => ("PageDown", 0x22u),
                "ARROWLEFT" => ("ArrowLeft", 0x25u), "ARROWUP" => ("ArrowUp", 0x26u), "ARROWRIGHT" => ("ArrowRight", 0x27u), "ARROWDOWN" => ("ArrowDown", 0x28u),
                _ => throw new ArgumentException("Choose a letter, number, function key, Space, or a navigation key.")
            };
        }
        var labels = new List<string>();
        foreach (var (flag, label) in new[] { (2u, "Ctrl"), (1u, "Alt"), (4u, "Shift"), (8u, "Win") }) if ((modifiers & flag) != 0) labels.Add(label);
        labels.Add(name);
        return new(string.Join('+', labels), modifiers, key);
    }
    internal static string Normalize(string? text) { try { return Parse(text).Shortcut; } catch (ArgumentException) { return Default; } }
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    internal static uint ReadModifiers() => (GetKeyState(0x11) < 0 ? 2u : 0u) | (GetKeyState(0x12) < 0 ? 1u : 0u)
        | (GetKeyState(0x10) < 0 ? 4u : 0u) | (GetKeyState(0x5B) < 0 || GetKeyState(0x5C) < 0 ? 8u : 0u);
    internal static Hotkey FromVirtualKey(uint key, uint modifiers)
    {
        var name = key switch
        {
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)key).ToString(),
            >= 0x70 and <= 0x87 => "F" + (key - 0x70 + 1),
            0x20 => "Space", 0x09 => "Tab", 0x0D => "Enter", 0x08 => "Backspace", 0x2E => "Delete", 0x2D => "Insert",
            0x24 => "Home", 0x23 => "End", 0x21 => "PageUp", 0x22 => "PageDown", 0x25 => "ArrowLeft", 0x26 => "ArrowUp", 0x27 => "ArrowRight", 0x28 => "ArrowDown",
            _ => throw new ArgumentException("Choose a letter, number, function key, Space, or a navigation key.")
        };
        return Parse(((modifiers & 2) != 0 ? "Ctrl+" : "") + ((modifiers & 1) != 0 ? "Alt+" : "")
            + ((modifiers & 4) != 0 ? "Shift+" : "") + ((modifiers & 8) != 0 ? "Win+" : "") + name);
    }
}

// Acquire a replacement first. If Windows rejects it, the current shortcut
// remains registered; no rollback race can strand the menu without its binding.
internal sealed class HotkeyBinding : IDisposable
{
    private readonly Func<int, uint, uint, bool> _register;
    private readonly Action<int> _unregister;
    internal Hotkey Current { get; private set; } = Hotkey.Parse(Hotkey.Default);
    internal int Id { get; private set; }
    internal bool Registered => Id != 0;
    internal HotkeyBinding(IntPtr window) : this((id, modifiers, key) => Native.RegisterHotKey(window, id, modifiers, key), id => Native.UnregisterHotKey(window, id)) { }
    internal HotkeyBinding(Func<int, uint, uint, bool> register, Action<int> unregister) { _register = register; _unregister = unregister; }
    internal bool TrySet(string shortcut, Action<Hotkey>? commit = null)
    {
        var next = Hotkey.Parse(shortcut);
        if (Registered && next == Current) { commit?.Invoke(next); return true; }
        var candidate = Id == 1 ? 2 : 1;
        if (!_register(candidate, next.Modifiers | 0x4000, next.Key)) return false;
        try { commit?.Invoke(next); }
        catch { _unregister(candidate); throw; }
        if (Registered) _unregister(Id);
        Id = candidate; Current = next; return true;
    }
    public void Dispose() { if (Registered) _unregister(Id); Id = 0; }
}
