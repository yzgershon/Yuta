using System.Runtime.InteropServices;

namespace Yuta.Services;

internal enum AudioAppKind { Other, Browser, WhatsApp, Zoom, Discord }
internal sealed record AudioProcess(uint Id, uint ParentId, string Name);
internal sealed record AudioSessionIdentity(uint ProcessId, uint OwnerId, string Name, AudioAppKind Kind, Guid Group)
{
    internal int VoicePriority => Kind is AudioAppKind.WhatsApp or AudioAppKind.Zoom or AudioAppKind.Discord ? 2 : Kind == AudioAppKind.Browser ? 1 : 0;
}

// Session PID alone is not necessarily the app: WebView and call helpers can own
// its audio. Resolve only recognized process families, never every child of Explorer.
internal static class AudioSessionIdentityResolver
{
    internal static AudioSessionIdentity Resolve(uint pid, string fallback, Guid group, IReadOnlyDictionary<uint, AudioProcess> processes)
    {
        var fallbackName = Path.GetFileNameWithoutExtension(fallback);
        var (fallbackKind, fallbackLabel) = App(fallbackName.ToLowerInvariant());
        var result = new AudioSessionIdentity(pid, pid, fallbackKind == AudioAppKind.Other ? fallbackName : fallbackLabel, fallbackKind, group);
        var visited = new HashSet<uint>();
        for (var current = pid; current != 0 && visited.Count < 24 && visited.Add(current);)
        {
            if (!processes.TryGetValue(current, out var process)) break;
            var name = Path.GetFileNameWithoutExtension(process.Name).ToLowerInvariant();
            var (kind, label) = App(name);
            if (kind != AudioAppKind.Other)
            {
                // A browser may be launched by a calling app. Only WebView helpers
                // inherit the host identity; standalone browsers keep their identity.
                if (result.Kind != AudioAppKind.Other && (kind != result.Kind || label != result.Name)) break;
                result = result with { OwnerId = current, Name = label, Kind = kind };
            }
            else if (name is not ("msedgewebview2" or "cpthost")) break;
            current = process.ParentId;
        }
        return result;
    }

    internal static bool ContributesToVoice(AudioSessionIdentity voice, AudioSessionIdentity candidate, bool capture)
    {
        // Browsers can mix many tabs in one render process. Use microphone levels
        // for Meet/browser calls so music from another tab cannot drive the bars.
        if (voice.Kind == AudioAppKind.Browser) return capture && voice.ProcessId == candidate.ProcessId;
        if (voice.ProcessId == candidate.ProcessId) return true;
        if (voice.Kind is not (AudioAppKind.WhatsApp or AudioAppKind.Zoom or AudioAppKind.Discord) || candidate.Kind != voice.Kind) return false;
        return voice.OwnerId == candidate.OwnerId || voice.Group != Guid.Empty && voice.Group == candidate.Group;
    }

    private static (AudioAppKind Kind, string Name) App(string name) => name switch
    {
        "whatsapp" or "whatsapp.root" => (AudioAppKind.WhatsApp, "WhatsApp"),
        "zoom" => (AudioAppKind.Zoom, "Zoom"),
        "discord" => (AudioAppKind.Discord, "Discord"),
        "chrome" => (AudioAppKind.Browser, "Chrome"),
        "msedge" => (AudioAppKind.Browser, "Edge"),
        "firefox" => (AudioAppKind.Browser, "Firefox"),
        "brave" => (AudioAppKind.Browser, "Brave"),
        "opera" => (AudioAppKind.Browser, "Opera"),
        "vivaldi" => (AudioAppKind.Browser, "Vivaldi"),
        "arc" => (AudioAppKind.Browser, "Arc"),
        _ => (AudioAppKind.Other, name)
    };

    internal static Dictionary<uint, AudioProcess> ReadProcesses()
    {
        var processes = new Dictionary<uint, AudioProcess>();
        var snapshot = CreateToolhelp32Snapshot(2, 0); // TH32CS_SNAPPROCESS
        if (snapshot == new IntPtr(-1)) return processes;
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), FileName = "" };
            if (!Process32FirstW(snapshot, ref entry)) return processes;
            do { processes[entry.ProcessId] = new(entry.ProcessId, entry.ParentProcessId, entry.FileName); }
            while (Process32NextW(snapshot, ref entry));
        }
        finally { Native.CloseHandle(snapshot); }
        return processes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId, Threads, ParentProcessId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string FileName;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
}
