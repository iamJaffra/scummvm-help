using LiveSplit.ComponentUtil;
using System.Diagnostics;
using System.Linq;
using System;
using Generated;

public class Groovie : ScummVM { }

public class Mohawk_Myst : ScummVM { }

public class Mohawk_Riven : ScummVM { }

public class SCI : ScummVM { }

public class Scumm : ScummVM { }

public class Sword1 : ScummVM 
{
    protected IntPtr scriptVars = IntPtr.Zero;
    public IntPtr ScriptVars => scriptVars;

    public override void Init()
    {
        base.Init();

        scriptVars = GetScriptVarsPointer(Game);
    }

    private IntPtr GetScriptVarsPointer(Process game)
    {
        Dbg.Info("Scanning for BS1 _scriptVars static address...");

        byte[] markerBytes =
        {
            0x66, 0x6E, 0x49, 0x73, 0x46, 0x61, 0x63, 0x69,
            0x6E, 0x67, 0x3A, 0x3A, 0x20, 0x54, 0x61, 0x72,
            0x67, 0x65, 0x74, 0x20, 0x69, 0x73, 0x6E, 0x27,
            0x74, 0x20, 0x61, 0x20, 0x6D, 0x65, 0x67, 0x61
        };

        var module = game.MainModule;
        var scanner = new SignatureScanner(game, module.BaseAddress, module.ModuleMemorySize);

        var target = is64Bit
            ? new SigScanTarget(3, "48 8D 0D")
            : new SigScanTarget(3, "C7 04 24");

        var results = scanner.ScanAll(target);

        foreach (var address in results)
        {
            IntPtr markerPtr = is64Bit
                ? address + 0x4 + game.ReadValue<int>(address)
                : game.ReadPointer(address);

            if (!BytesEqual(game, markerPtr, markerBytes))
                continue;

            IntPtr found = is64Bit
                ? ScanForWriteOperand64(game, address)
                : ScanForWriteOperand32(game, address);

            if (found != IntPtr.Zero)
            {
                ScriptVarsPointerFoundMessage(found);
                return found;
            }
        }

        Dbg.Info("  => _scriptVars static address not found.");
        return IntPtr.Zero;
    }

    private IntPtr ScanForWriteOperand64(Process game, IntPtr hitAddress)
    {
        const int backwardsRange = 0x50;

        long start = hitAddress.ToInt64();
        long end = start - backwardsRange;

        for (long ptr = start; ptr > end; ptr--)
        {
            byte[] b = game.ReadBytes((IntPtr)ptr, 2);
            if (b[0] == 0x89 && b[1] == 0x05)
            {
                int rel = game.ReadValue<int>((IntPtr)ptr + 2);
                long absolute = ptr + 6 + rel;
                return (IntPtr)absolute;
            }
        }

        return IntPtr.Zero;
    }

    private IntPtr ScanForWriteOperand32(Process game, IntPtr hitAddress)
    {
        int start = hitAddress.ToInt32();
        int end = (version == "2.0.0")
            ? start + 0x100
            : start - 0x50;

        int step = (start < end) ? 1 : -1;

        for (int addr = start; addr != end; addr += step)
        {
            byte[] b = game.ReadBytes((IntPtr)addr, 1);
            if (b[0] == 0xA3)
            {
                int absolute = game.ReadValue<int>((IntPtr)addr + 1);
                return (IntPtr)absolute;
            }
        }

        return IntPtr.Zero;
    }

    protected void ScriptVarsPointerFoundMessage(IntPtr pointer)
    {
        Dbg.Info($"  => _scriptVars static address found at 0x{pointer.ToInt64():X}");
        Dbg.Info();
        Dbg.Info($"  => Assigned _scriptVars pointer to vars.ScummVM.ScriptVars");
        Dbg.Info();
    }

    protected override (IntPtr baseAddress, int[] offsets) ResolvePath(params object[] path)
    {
        if (path.Length > 0 && path[0] is string s && s == "_scriptVars")
        {
            if (path.Length > 2)
                throw new ArgumentException("_scriptVars path must contain no more than one offset.");

            if (path[1] is not int offset)
                throw new ArgumentException("_scriptVars offset must be an integer.");

            IntPtr addr = scriptVars + offset;

            if (logResolvedPaths)
            {
                Dbg.Info($"Resolved path: 0x{addr.ToString("X")}");
            }
            return (addr, Array.Empty<int>());
        }

        return base.ResolvePath(path);
    }
}

public class Toon : ScummVM { }