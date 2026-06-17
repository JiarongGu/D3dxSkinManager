// win-input — background native mouse input to the real D3dxSkinManager window (self-verify native surfaces).
//
// Technique (from MaaNTE/MaaFramework's maa_keyboard.py): resolve the target HWND, then PostMessage mouse
// messages to it — this queues to that window's UI-thread message loop WITHOUT moving the real cursor or
// stealing focus, so it works even while D3dxSkinManager is occluded by the agent console (the case that breaks any
// SendInput/cursor approach). A WM_ACTIVATE(WA_CLICKACTIVE) "wake" precedes input, like MaaNTE.
//
// We recurse ChildWindowFromPointEx from the top-level D3dxSkinManager window down to the LEAF child at the target
// point (occlusion/z-order-independent — it walks the parent's child list), so messages land on the actual
// control under the point: the LibVLC overlay OR the enhance EnhancePreviewSurface (a WinForms Panel whose
// WndProc turns WM_LBUTTONDOWN/UP/RBUTTONUP into OnMouseDown/Up — firing the Stop/Pause pills + the menu).
//
// Coords are FRACTIONS (0..1) of the top-level window's CLIENT area, so they're resolution-independent.
//   win-input click  <x> <y>            left click   (down+up)
//   win-input rclick <x> <y>            right click  (down+up → context menu)
//   win-input move   <x> <y>            mouse move
//   win-input drag   <x1> <y1> <x2> <y2>  press, move, release (the wipe-divider drag)
//   --proc <Name>  target process (default D3dxSkinManager)   |   --hwnd 0x1234  explicit top-level HWND

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class WinInput
{
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern IntPtr ChildWindowFromPointEx(IntPtr parent, POINT pt, uint flags);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern int GetWindowLongW(IntPtr h, int index);
    private static readonly IntPtr DPI_PER_MONITOR_AWARE_V2 = new IntPtr(-4);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private static bool IsLayered(IntPtr h) => (GetWindowLongW(h, GWL_EXSTYLE) & WS_EX_LAYERED) != 0;

    private static string Text(IntPtr h) { var sb = new StringBuilder(256); GetWindowTextW(h, sb, 256); return sb.ToString(); }
    private static string Cls(IntPtr h) { var sb = new StringBuilder(256); GetClassNameW(h, sb, 256); return sb.ToString(); }

    private const uint WM_ACTIVATE = 0x0006, WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
    private const int WA_CLICKACTIVE = 2, MK_LBUTTON = 0x0001, MK_RBUTTON = 0x0002, CWP_SKIPINVISIBLE = 0x0001;

    private static int Main(string[] args)
    {
        // Match D3dxSkinManager's per-monitor-v2 DPI awareness so GetClientRect + mouse lParam are in the SAME
        // (physical) pixel space the WinForms controls see — otherwise a DPI-unaware process gets virtualized
        // (halved at 200%) coords and clicks land at the wrong place.
        try { SetProcessDpiAwarenessContext(DPI_PER_MONITOR_AWARE_V2); } catch { /* older OS */ }

        if (args.Length < 1) { Console.Error.WriteLine("usage: win-input <click|rclick|move|drag|list> <x> <y> [x2 y2] [--proc N] [--hwnd 0x..]"); return 2; }
        string action = args[0];
        string proc = ArgVal(args, "--proc") ?? "D3dxSkinManager";
        string? hwndArg = ArgVal(args, "--hwnd");
        bool wantChrome = Array.IndexOf(args, "--chrome") >= 0; // target the LAYERED player chrome window
        var nums = NumArgs(args);

        if (action == "list") { ListWindows(proc); return 0; }

        IntPtr top = hwndArg != null
            ? new IntPtr(Convert.ToInt64(hwndArg.Replace("0x", ""), 16))
            : wantChrome ? (FindLayeredWindow(proc) is { } c && c != IntPtr.Zero ? c : FindTopWindow(proc))
            : FindTopWindow(proc);
        if (top == IntPtr.Zero) { Console.Error.WriteLine($"win-input: no window for process '{proc}'"); return 1; }
        if (!GetClientRect(top, out var rc) || rc.Right <= 0) { Console.Error.WriteLine("win-input: empty client rect"); return 1; }
        int W = rc.Right, H = rc.Bottom;

        POINT P(double fx, double fy) => new POINT { X = (int)Math.Round(fx * W), Y = (int)Math.Round(fy * H) };

        switch (action)
        {
            case "click": Click(top, P(nums[0], nums[1]), false); break;
            case "rclick": Click(top, P(nums[0], nums[1]), true); break;
            case "move": { var (h, pt) = Leaf(top, P(nums[0], nums[1])); Post(h, WM_MOUSEMOVE, 0, pt); break; }
            case "drag": Drag(top, P(nums[0], nums[1]), P(nums[2], nums[3])); break;
            default: Console.Error.WriteLine($"win-input: unknown action '{action}'"); return 2;
        }
        Console.WriteLine($"win-input: {action} ok on hwnd=0x{top.ToInt64():X} client={W}x{H}");
        return 0;
    }

    // Walk the child-window list to the LEAF control under the point (parent-client coords), returning that
    // hwnd + the point in ITS client coords. Occlusion-independent (uses the parent's child list, not z-order).
    private static (IntPtr hwnd, POINT pt) Leaf(IntPtr top, POINT ptInTopClient)
    {
        IntPtr h = top; POINT pt = ptInTopClient;
        for (int i = 0; i < 16; i++)
        {
            POINT scr = pt; ClientToScreen(h, ref scr);
            IntPtr child = ChildWindowFromPointEx(h, pt, CWP_SKIPINVISIBLE);
            if (child == IntPtr.Zero || child == h) break;
            POINT cpt = scr; ScreenToClient(child, ref cpt);
            h = child; pt = cpt;
        }
        return (h, pt);
    }

    private static IntPtr LParam(POINT p) => new IntPtr((p.Y << 16) | (p.X & 0xFFFF));
    private static void Post(IntPtr h, uint msg, int w, POINT pt) => PostMessage(h, msg, new IntPtr(w), LParam(pt));

    private static void Click(IntPtr top, POINT ptTop, bool right)
    {
        var (h, pt) = Leaf(top, ptTop);
        Console.WriteLine($"  leaf=0x{h.ToInt64():X} class='{Cls(h)}' at ({pt.X},{pt.Y})");
        SendMessage(h, WM_ACTIVATE, new IntPtr(WA_CLICKACTIVE), IntPtr.Zero); // MaaNTE background-wake
        Post(h, WM_MOUSEMOVE, right ? MK_RBUTTON : MK_LBUTTON, pt);
        Thread.Sleep(15);
        Post(h, right ? WM_RBUTTONDOWN : WM_LBUTTONDOWN, right ? MK_RBUTTON : MK_LBUTTON, pt);
        Thread.Sleep(40);
        Post(h, right ? WM_RBUTTONUP : WM_LBUTTONUP, 0, pt);
    }

    private static void Drag(IntPtr top, POINT a, POINT b)
    {
        var (h, pa) = Leaf(top, a);
        var (_, pb) = Leaf(top, b); // same surface assumed; pb in that leaf's coords
        Console.WriteLine($"  leaf=0x{h.ToInt64():X} class='{Cls(h)}' ({pa.X},{pa.Y})->({pb.X},{pb.Y})");
        SendMessage(h, WM_ACTIVATE, new IntPtr(WA_CLICKACTIVE), IntPtr.Zero);
        Post(h, WM_MOUSEMOVE, MK_LBUTTON, pa);
        Post(h, WM_LBUTTONDOWN, MK_LBUTTON, pa);
        const int steps = 12;
        for (int i = 1; i <= steps; i++)
        {
            var pt = new POINT { X = pa.X + (pb.X - pa.X) * i / steps, Y = pa.Y + (pb.Y - pa.Y) * i / steps };
            Post(h, WM_MOUSEMOVE, MK_LBUTTON, pt);
            Thread.Sleep(15);
        }
        Post(h, WM_LBUTTONUP, 0, pb);
    }

    // Prefer the window whose TITLE matches the process name (the main form's Text="D3dxSkinManager") — its largest
    // visible window can be a helper/layered-chrome top-level. Fall back to the largest titled window.
    private static IntPtr FindTopWindow(string proc)
    {
        var pids = ProcPids(proc);
        if (pids.Count == 0) return IntPtr.Zero;
        IntPtr titled = IntPtr.Zero, biggest = IntPtr.Zero; long bestArea = 0;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out var pid);
            if (!pids.Contains(pid)) return true;
            if (!GetClientRect(h, out var rc) || rc.Right <= 0) return true;
            long area = (long)rc.Right * rc.Bottom;
            if (string.Equals(Text(h), proc, StringComparison.OrdinalIgnoreCase) && area > 100) titled = h;
            if (area > bestArea) { bestArea = area; biggest = h; }
            return true;
        }, IntPtr.Zero);
        return titled != IntPtr.Zero ? titled : biggest;
    }

    // The native player CHROME is a per-pixel-alpha LAYERED window (WS_EX_LAYERED) — unique among the
    // process's visible top-level windows (the main form isn't layered), so this targets it reliably for
    // clicking the player controls (the flaky empty-title heuristic picked the wrong window).
    private static IntPtr FindLayeredWindow(string proc)
    {
        var pids = ProcPids(proc);
        IntPtr found = IntPtr.Zero; long bestArea = 0;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h) || !IsLayered(h)) return true;
            GetWindowThreadProcessId(h, out var pid);
            if (!pids.Contains(pid)) return true;
            if (GetClientRect(h, out var rc) && (long)rc.Right * rc.Bottom > bestArea) { bestArea = (long)rc.Right * rc.Bottom; found = h; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static void ListWindows(string proc)
    {
        var pids = ProcPids(proc);
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out var pid);
            if (!pids.Contains(pid)) return true;
            GetClientRect(h, out var rc);
            Console.WriteLine($"hwnd=0x{h.ToInt64():X} client={rc.Right}x{rc.Bottom} layered={(IsLayered(h) ? 1 : 0)} class='{Cls(h)}' title='{Text(h)}'");
            return true;
        }, IntPtr.Zero);
    }

    private static System.Collections.Generic.HashSet<uint> ProcPids(string proc)
    {
        var pids = new System.Collections.Generic.HashSet<uint>();
        foreach (var p in Process.GetProcessesByName(proc)) pids.Add((uint)p.Id);
        return pids;
    }

    private static string? ArgVal(string[] args, string flag)
    {
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static double[] NumArgs(string[] args)
    {
        var list = new System.Collections.Generic.List<double>();
        for (int i = 1; i < args.Length; i++)
            if (double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) list.Add(v);
        while (list.Count < 4) list.Add(0);
        return list.ToArray();
    }
}
