#define DEBUG
using System.Diagnostics;

internal class Dbg
{
    public static void Info()
    {
        Debug.WriteLine("[ScummVM-Help]");
    }

    public static void Info(string msg)
    {
        Debug.WriteLine($"[ScummVM-Help] {msg}");
    }
}
