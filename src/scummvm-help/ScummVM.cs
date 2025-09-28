using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using System.Threading.Tasks;
using System.Threading;
using Generated;

public class ScummVM
{
    // g_engine pointer
    protected IntPtr g_engine = IntPtr.Zero;
    public IntPtr GEngine => g_engine;

    // The name of the ScummVM engine the game runs on
    protected string engine = null;

    // Game process
    internal static volatile Process Game = null;

    // ScummVM ver
    internal string version = null;
    internal bool is64Bit = true;

    // ASL script
    internal static dynamic script = null;
    internal static dynamic Vars;

    // Watchers
    protected readonly MemoryWatcherList watchers = new MemoryWatcherList();
    internal static IDictionary<string, dynamic> Current => script.State.Data;

    private readonly Dictionary<string, object> rootDict;

    private Func<dynamic, bool> tryLoad;
    private CancellationTokenSource _tryLoadCts;
    private volatile bool loaded = false; 

    public ScummVM()
    {
        try
        {
            engine = GetType().Name.ToLower();
            rootDict = Offsets.Data;

            Dbg.Info("Launching ScummVM-Help...");

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
        
        version = GetVersion(Game);
        g_engine = GetEnginePointer(Game);
    }

    protected string GetVersion(Process game)
    {
        Dbg.Info("Determining ScummVM version...");

        var fileVersion = game.MainModule.FileVersionInfo.FileVersion;
        is64Bit = Game.Is64Bit();

        var ver = "";

        if (fileVersion == "2.1.0git")
        {
            byte[] mBytes = [
                0x32, 0x2E, 0x31, 0x2E, 0x30, 0x67, 0x69, 0x74,
                0x20, 0x28, 0x4F, 0x63, 0x74, 0x20, 0x32, 0x33,
                0x20, 0x32, 0x30, 0x31, 0x38, 0x20, 0x31, 0x36,
                0x3A, 0x33, 0x30, 0x3A, 0x33, 0x38, 0x29
            ];

            byte[] rBytes = [
                0x32, 0x2E, 0x31, 0x2E, 0x30, 0x67, 0x69, 0x74,
                0x20, 0x28, 0x4F, 0x63, 0x74, 0x20, 0x20, 0x32,
                0x20, 0x32, 0x30, 0x31, 0x38, 0x20, 0x32, 0x30,
                0x3A, 0x30, 0x33, 0x3A, 0x34, 0x30, 0x29
            ];

            var module = game.MainModule;
            var scanner = new SignatureScanner(game, module.BaseAddress, module.ModuleMemorySize);

            if (scanner.Scan(new SigScanTarget(0, mBytes)) != IntPtr.Zero)
            {
                ver = "Myst 25th Anniversary (32-bit)";
            }
            else if (scanner.Scan(new SigScanTarget(0, rBytes)) != IntPtr.Zero)
            {
                ver = "Riven 25th Anniversary (32-bit)";
            }
        }
        else if (fileVersion == "2.9.0")
        {
            byte[] vBytes = [
                0x32, 0x2E, 0x39, 0x2E, 0x30, 0x20, 0x28, 0x4A,
                0x75, 0x6E, 0x20, 0x32, 0x33, 0x20, 0x32, 0x30,
                0x32, 0x35, 0x20, 0x30, 0x39, 0x3A, 0x33, 0x31,
                0x3A, 0x32, 0x36, 0x29
            ];

            var module = game.MainModule;
            var scanner = new SignatureScanner(game, module.BaseAddress, module.ModuleMemorySize);

            if (scanner.Scan(new SigScanTarget(0, vBytes)) != IntPtr.Zero)
            {
                ver = "Riven 25th Anniversary (64-bit)";
            }
        }
        
        if (ver == "")
        {
            ver = fileVersion.ToString();
        }
        
        Dbg.Info("  => Detected version " + ver + " (" + (is64Bit ? "64" : "32") + "-bit)");
        Dbg.Info("");

        return ver;
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
                    long searchEnd = (long)address - 0x200;

                    for (long addr = searchStart; addr > searchEnd; addr--)
                    {
                        byte[] b = game.ReadBytes((IntPtr)addr, 3);
                        if ((b[0] == 0x48 || b[0] == 0x4C) && b[1] == 0x89)
                        {
                            if (b[2] == 0x05 ||
                                b[2] == 0x0D ||
                                b[2] == 0x15 ||
                                b[2] == 0x1D ||
                                b[2] == 0x25 ||
                                b[2] == 0x2D ||
                                b[2] == 0x35 ||
                                b[2] == 0x3D)
                            {
                                int rel = game.ReadValue<int>((IntPtr)addr + 3);
                                long g_engineAddr = addr + 7 + rel;

                                Dbg.Info($"  => g_engine found at 0x{g_engineAddr:X}");
                                Dbg.Info();
                                Dbg.Info("  => Set g_engine pointer to vars.ScummVM.GEngine");
                                Dbg.Info();

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
                    int searchEnd = (int)address - 0x200;

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
        return new DeepPointer(g_engine, offsets).Deref<T>(Game);
    }

    public string ReadString(params object[] path)
    {
        int[] offsets = ResolvePath(path);
        offsets = offsets.Concat([0x0]).ToArray();
        return new DeepPointer(g_engine, offsets).DerefString(Game, 32);
    }

    public string ReadString(int length, params object[] path)
    {
        int[] offsets = ResolvePath(path);
        return new DeepPointer(g_engine, offsets).DerefString(Game, length);
    }

    private int[] ResolvePath(params object[] path)
    {
        if (path == null || path.Length == 0)
            throw new ArgumentException("Path must contain at least one element.");

        var engineNode = GetEngineNode(rootDict, is64Bit ? "64-bit" : "32-bit", version, engine);

        var derefOffsets = new List<int>();

        bool isInlineStruct = false;

        foreach (var part in path)
        {
            if (part is string key)
            {
                engineNode = (Dictionary<string, object>)engineNode[key];
                if (!engineNode.TryGetValue("offset", out var offsetObj))
                    continue;

                string raw = offsetObj.ToString();
                int offset = ParseOffset(raw);

                bool isInline = engineNode.TryGetValue("inline", out var inlineObj) &&
                    bool.TryParse(inlineObj.ToString(), out var inlineParsed) &&
                    inlineParsed;

                if (isInlineStruct)
                {
                    if (derefOffsets.Count == 0)
                        throw new InvalidOperationException("Inline merge with empty list");

                    derefOffsets[derefOffsets.Count - 1] += offset;
                    isInlineStruct = false;
                }
                else
                {
                    derefOffsets.Add(offset);
                }

                isInlineStruct = isInline;
            }
            else if (part is int i)
            {
                if (isInlineStruct)
                {
                    if (derefOffsets.Count == 0)
                        throw new InvalidOperationException("Inline merge with empty list");

                    derefOffsets[derefOffsets.Count - 1] += i;
                    isInlineStruct = false;
                }
                else
                {
                    derefOffsets.Add(i);
                }
            }
        }

        return derefOffsets.ToArray();
    }

    private static int ParseOffset(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new FormatException("Offset string is empty.");

        raw = raw.Trim();
        return raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(raw, 16)
            : int.Parse(raw);
    }

    private static Dictionary<string, object> GetEngineNode(Dictionary<string, object> rootDict, string bitness, string version, string engine)
    {
        if (!rootDict.TryGetValue("scummvm", out var scummvmObj) ||
            scummvmObj is not Dictionary<string, object> scummvmDict)
            throw new InvalidOperationException("'scummvm' key missing or invalid in YAML data.");

        if (!scummvmDict.TryGetValue(bitness, out var bitObj) ||
            bitObj is not Dictionary<string, object> bitDict)
            throw new InvalidOperationException($"Bitness '{bitness}' not found under 'scummvm'.");

        if (!bitDict.TryGetValue(version, out var verObj) ||
            verObj is not Dictionary<string, object> versionDict)
            throw new InvalidOperationException($"Version '{version}' not found under '{bitness}'.");

        if (!versionDict.TryGetValue(engine, out var engineObj) ||
            engineObj is not Dictionary<string, object> engineNode)
            throw new InvalidOperationException($"Engine '{engine}' not found in version '{version}' ({bitness}).");

        return engineNode;
    }

    private static void DumpDictionary(Dictionary<string, object> dict, string indent = "")
    {
        foreach (var kvp in dict)
        {
            if (kvp.Value is Dictionary<string, object> subDict)
            {
                Dbg.Info($"{indent}{kvp.Key}:");
                DumpDictionary(subDict, indent + "  ");
            }
            else
            {
                Dbg.Info($"{indent}{kvp.Key}: {kvp.Value}");
            }
        }
    }

    public Func<dynamic, bool> TryLoad
    {
        get => tryLoad;
        set
        {
            tryLoad = value;

            try
            {
                _tryLoadCts?.Cancel();
                _tryLoadCts?.Dispose();
                _tryLoadCts = null;
            }
            catch { }

            if (value == null)
            {
                return;
            }

            _tryLoadCts = new CancellationTokenSource();
            var token = _tryLoadCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && !loaded)
                    {
                        var g = is64Bit
                            ? Game.ReadValue<ulong>(g_engine)
                            : Game.ReadValue<uint>(g_engine);

                        if (g != 0)
                        {
                            try
                            {
                                bool success = tryLoad!(this);
                                Dbg.Info($"[ScummVM-Help] TryLoad returned {success}.");
                                if (success)
                                {
                                    loaded = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Dbg.Info($"[ScummVM-Help] TryLoad threw: {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                        else
                        {
                            Dbg.Info("[ScummVM-Help] Waiting for game engine to be running...");
                        }

                        try { await Task.Delay(3000, token); }
                        catch (TaskCanceledException) { break; }
                    }
                }
                finally
                {
                    // Dbg.Info($"[TryLoad] Polling ended (task {Task.CurrentId}). loaded={loaded}");
                }
            }, token);
        }
    }
}
