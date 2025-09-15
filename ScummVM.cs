using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using Generated;
using System.Text.Json.Nodes;
using System.IO;

public class ScummVM
{
    // g_engine pointer
    protected IntPtr g_engine = IntPtr.Zero;
    public IntPtr GEngine => g_engine;

    // The name of the ScummVM engine the game runs on
    protected string engine = null;

    // Game process
    internal static volatile Process Game = null;

    // ScummVM version
    internal string version = null;
    internal bool is64Bit = true;

    // ASL script
    internal static dynamic script = null;
    internal static dynamic Vars;

    // Watchers
    protected readonly MemoryWatcherList watchers = new MemoryWatcherList();
    internal static IDictionary<string, dynamic> Current => script.State.Data;

    private readonly Dictionary<string, object> rootDict;

    internal static readonly string[] EngineList =
    {
        "SCI",
        "Mohawk_Myst",
        "Mohawk_Riven"
    };

    public ScummVM()
    {
        try
        {
            engine = GetType().Name.ToLower();
            rootDict = Generated.Offsets.Data;

            Dbg.Info("Launching ScummVM-Help...");

            if (engine == "scummvm")
            {
                Dbg.Info();
                Dbg.Info("Please select an engine.");
                Dbg.Info("The following engines are currently supported:");

                foreach (var e in EngineList)
                {
                    Dbg.Info($"  - {e}");
                }
                Dbg.Info();
            }

            GetScript();

            if (script != null)
            {
                Vars = script.Vars;
                Vars.ScummVM = this;

                Dbg.Info("  => Set helper to vars.ScummVM");
            }
            else
            {
                Dbg.Info("Error: Could not attach to the script.");
            }
        }
        catch (Exception ex)
        {
            Dbg.Info($"Constructor failed: {ex}");
            throw;
        }

    }

    public void GetScript()
    {
        dynamic timerForm = Application.OpenForms["TimerForm"];

        if (timerForm == null)
        {
            return;
        }

        LiveSplitState currentState = timerForm.CurrentState;
        foreach (var layoutComponent in currentState.Layout.LayoutComponents)
        {
            dynamic component = layoutComponent.Component;
            if (component.GetType().Name.Contains("ASLComponent"))
            {
                script = component.Script;
                if (script != null)
                {
                    break;
                }
            }
        }

        if (script == null && currentState?.Run?.AutoSplitter != null && currentState.Run.AutoSplitter.IsActivated)
        {
            dynamic component = currentState.Run.AutoSplitter.Component;
            script = component.Script;
        }
    }

    public void Init()
    {
        if (script != null)
        {
            FieldInfo gameField = script.GetType().GetField("_game", BindingFlags.NonPublic | BindingFlags.Instance);
            var gameInstance = gameField?.GetValue(script);

            Game = (Process)gameInstance;
        }

        Dbg.Info("Initializing ScummVM-Help...");
        Dbg.Info("Determining ScummVM version...");

        var fileVersion = Game.MainModule.FileVersionInfo.FileVersion;
        is64Bit = Game.Is64Bit();

        version = fileVersion.ToString() + " (" + (is64Bit ? "64" : "32") + "-bit)";

        Dbg.Info("  => Detected version " + version);
        Dbg.Info("");

        g_engine = GetEnginePointer(Game);
    }

    protected IntPtr GetEnginePointer(Process game)
    {
        Dbg.Info("Scanning for g_engine...");

        byte[] wBytes = [
            0x53, 0x6F, 0x75, 0x6E, 0x64, 0x20, 0x69, 0x6E,
            0x69, 0x74, 0x69, 0x61, 0x6C, 0x69, 0x7A, 0x61,
            0x74, 0x69, 0x6F, 0x6E, 0x20, 0x66, 0x61, 0x69,
            0x6C, 0x65, 0x64, 0x2E, 0x20, 0x54, 0x68, 0x69,
            0x73, 0x20, 0x6D, 0x61, 0x79, 0x20, 0x63, 0x61,
            0x75, 0x73, 0x65, 0x20, 0x73, 0x65, 0x76, 0x65,
            0x72, 0x65, 0x20, 0x70, 0x72, 0x6F, 0x62, 0x6C,
            0x65, 0x6D, 0x73, 0x20, 0x69, 0x6E, 0x20, 0x73,
            0x6F, 0x6D, 0x65, 0x20, 0x67, 0x61, 0x6D, 0x65,
            0x73
        ];

        var module = game.MainModule;
        var scanner = new SignatureScanner(game, module.BaseAddress, module.ModuleMemorySize);

        if (is64Bit)
        {
            var trg = new SigScanTarget(3, "48 8D 0D");
            var results = scanner.ScanAll(trg);

            foreach (var address in results)
            {
                byte[] s = game.ReadBytes(address + 0x4 + game.ReadValue<int>(address), wBytes.Length);

                if (s != null && s.SequenceEqual(wBytes))
                {
                    long searchStart = (long)address;
                    long searchEnd = (long)address - 0x100;

                    for (long addr = searchStart; addr > searchEnd; addr--)
                    {
                        byte[] b = game.ReadBytes((IntPtr)addr, 3);
                        if (b[0] == 0x48 && b[1] == 0x89)
                        {
                            if (b[2] == 0x05 ||
                                b[2] == 0x0D ||
                                b[2] == 0x15 ||
                                b[2] == 0x1D ||
                                b[2] == 0x2D ||
                                b[2] == 0x35 ||
                                b[2] == 0x3D)
                            {
                                int rel = game.ReadValue<int>((IntPtr)addr + 3);
                                long g_engineAddr = addr + 7 + rel;

                                Dbg.Info($"  => g_engine found at 0x{g_engineAddr:X}");
                                Dbg.Info("  => Set g_engine pointer to vars.ScummVM.GEngine");

                                return (IntPtr)g_engineAddr;
                            }
                        }
                    }
                }
            }
        }
        else if (!is64Bit)
        {
            var trg = new SigScanTarget(3, "C7 04 24");
            var results = scanner.ScanAll(trg);

            foreach (var address in results)
            {
                byte[] s = game.ReadBytes(game.ReadPointer(address), wBytes.Length);

                if (s != null && s.SequenceEqual(wBytes))
                {
                    int searchStart = (int)address;
                    int searchEnd = (int)address - 0x100;

                    for (int addr = searchStart; addr > searchEnd; addr--)
                    {
                        byte[] b = game.ReadBytes((IntPtr)addr, 2);
                        if (b[0] == 0x89 && b[1] == 0x1D)
                        {
                            int g_engineAddr = game.ReadValue<int>((IntPtr)addr + 2);

                            Dbg.Info("  => g_engine found at 0x" + g_engineAddr.ToString("X"));

                            return (IntPtr)g_engineAddr;
                        }
                    }
                }
            }
        }

        Dbg.Info("  => g_engine not found.");
        return IntPtr.Zero;
    }

    public void Update()
    {
        watchers.UpdateAll(Game);

        MapPointers();
    }

    public void MapPointers()
    {
        foreach (MemoryWatcher watcher in watchers)
        {
            Current[watcher.Name] = watcher.Current;
        }
    }

    public MemoryWatcher this[string name]
    {
        get
        {
            if (watchers.FirstOrDefault(w => w.Name == name) is MemoryWatcher watcher)
            {
                return watcher;
            }

            throw new KeyNotFoundException($"The given watcher '{name}' was not present in the helper.");
        }
        set
        {
            RemoveWatcher(name);

            if (value is null)
            {
                return;
            }

            value.Name = name;
            watchers.Add(value);
        }
    }

    public void RemoveWatcher(string name)
    {
        int index = watchers.FindIndex(w => w.Name == name);

        if (index > -1)
        {
            watchers.RemoveAt(index);
        }
    }

    public MemoryWatcher<T> Watch<T>(params object[] path) where T : unmanaged
    {
        int[] offsets = ResolvePath(path);
        return new MemoryWatcher<T>(new DeepPointer(g_engine, offsets));
    }

    public T Read<T>(params object[] path) where T : unmanaged
    {
        int[] offsets = ResolvePath(path);
        var dp = new DeepPointer(g_engine, offsets);
        return dp.Deref<T>(Game);
    }

    private int[] ResolvePath(params object[] path)
    {
        if (path == null || path.Length == 0)
            throw new ArgumentException("Path must contain at least one element.");

        bool sawInt = false;
        foreach (var p in path)
        {
            if (p is int) sawInt = true;
            else if (p is string)
            {
                if (sawInt)
                    throw new ArgumentException("Strings cannot appear after ints in path.");
            }
            else
                throw new ArgumentException("Path may only contain strings or ints.");
        }

        var scummvmList = (List<object>)rootDict["scummvm"];
        if (scummvmList == null || scummvmList.Count == 0)
            throw new InvalidOperationException("'scummvm' key missing or empty in YAML data.");

        var engineDict = (Dictionary<string, object>)scummvmList[0];
        if (!engineDict.TryGetValue(engine, out var engineObj))
            throw new InvalidOperationException($"Engine '{engine}' not found.");

        var engineNode = (Dictionary<string, object>)engineObj;

        var derefOffsets = new List<int>();

        foreach (var part in path)
        {
            if (part is string key)
            {
                engineNode = (Dictionary<string, object>)engineNode[key];
                if (engineNode == null)
                    throw new KeyNotFoundException($"Key '{key}' not found under engine '{engine}'.");

                var offsetNode = engineNode["offset"];
                if (offsetNode != null)
                {
                    string raw = offsetNode.ToString().Trim();

                    if (raw.StartsWith("+"))
                    {
                        int v = ParseOffset(raw);
                        if (derefOffsets.Count == 0)
                            throw new InvalidOperationException("Cannot apply '+' to nonexistent previous offset.");
                        derefOffsets[derefOffsets.Count - 1] += v;
                    }
                    else
                    {
                        derefOffsets.Add(ParseOffset(raw));
                    }
                }
            }
            else if (part is int i)
            {
                derefOffsets.Add(i);
            }
        }

        return derefOffsets.ToArray();
    }

    private static int ParseOffset(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new FormatException("Offset string is empty.");

        raw = raw.Trim();
        if (raw.StartsWith("+"))
            raw = raw.Substring(1).Trim();

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(raw, 16);
        }
        else
        {
            return int.Parse(raw);
        }
    }
}
