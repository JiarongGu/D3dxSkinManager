// wgc-shot — occlusion-immune window capture (Windows.Graphics.Capture).
//   wgc-shot --proc D3dxSkinManager --out path.png        capture a process's main window
//   wgc-shot --hwnd 0x12345 --out path.png       capture a specific HWND
// Works even when the target window is BEHIND another window (e.g. the agent console covering
// D3dxSkinManager while a permission prompt is up) — WGC captures the window's own composed frame, not the
// screen region. Includes child windows (the LibVLC video surface) via DWM composition.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

internal static class Program
{
    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion,
        out IntPtr device, out int featureLevel, out IntPtr immediateContext);

    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D11_SDK_VERSION = 7;
    private static Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string src, int length, out IntPtr hstring);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr hstring);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid, out IntPtr result);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid, out IntPtr result);
    }
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [STAThread]
    private static int Main(string[] args)
    {
        string proc = "D3dxSkinManager", outPath = "wgc.png";
        long hwndArg = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--proc" && i + 1 < args.Length) proc = args[++i];
            else if (args[i] == "--out" && i + 1 < args.Length) outPath = args[++i];
            else if (args[i] == "--hwnd" && i + 1 < args.Length)
                hwndArg = args[i + 1].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt64(args[++i], 16) : long.Parse(args[++i]);
        }

        IntPtr hwnd;
        if (hwndArg != 0) hwnd = new IntPtr(hwndArg);
        else
        {
            var p = Process.GetProcessesByName(proc).FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);
            if (p == null) { Console.Error.WriteLine($"wgc-shot: no main window for process '{proc}'"); return 2; }
            hwnd = p.MainWindowHandle;
        }
        if (!IsWindow(hwnd)) { Console.Error.WriteLine($"wgc-shot: not a window: 0x{hwnd.ToInt64():X}"); return 2; }
        if (IsIconic(hwnd)) Console.Error.WriteLine("wgc-shot: WARNING window is minimized — WGC may produce an empty frame");

        // 1. D3D11 device (BGRA support required by WGC) → IDirect3DDevice
        int dhr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            IntPtr.Zero, 0, D3D11_SDK_VERSION, out IntPtr d3dDevicePtr, out _, out IntPtr ctxPtr);
        if (dhr < 0) { Console.Error.WriteLine($"wgc-shot: D3D11CreateDevice failed 0x{dhr:X}"); return 3; }
        Marshal.QueryInterface(d3dDevicePtr, ref IID_IDXGIDevice, out IntPtr dxgiPtr);
        CreateDirect3D11DeviceFromDXGIDevice(dxgiPtr, out IntPtr inspectable);
        var device = MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
        Marshal.Release(inspectable);
        Marshal.Release(dxgiPtr);
        Marshal.Release(d3dDevicePtr);
        if (ctxPtr != IntPtr.Zero) Marshal.Release(ctxPtr);

        // 2. GraphicsCaptureItem for the window via the interop activation factory
        const string cls = "Windows.Graphics.Capture.GraphicsCaptureItem";
        WindowsCreateString(cls, cls.Length, out var hs);
        var interopIid = typeof(IGraphicsCaptureItemInterop).GUID;
        RoGetActivationFactory(hs, ref interopIid, out var factoryPtr);
        WindowsDeleteString(hs);
        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
        Marshal.Release(factoryPtr);
        var itemIid = GraphicsCaptureItemIid;
        interop.CreateForWindow(hwnd, ref itemIid, out var itemPtr);
        var item = MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr);
        Marshal.Release(itemPtr);

        // 3. Frame pool + session (free-threaded → no dispatcher needed in a console app)
        var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        var session = framePool.CreateCaptureSession(item);
        try { session.IsCursorCaptureEnabled = false; } catch { /* older OS */ }
        try { session.IsBorderRequired = false; } catch { /* needs the capability/OS */ }

        Direct3D11CaptureFrame? frame = null;
        var got = new ManualResetEventSlim();
        framePool.FrameArrived += (s, _) =>
        {
            var f = s.TryGetNextFrame();
            // skip the first couple frames (often blank as the pool warms up) — keep the 3rd
            if (frame == null) { frame = f; got.Set(); }
            else f.Dispose();
        };
        session.StartCapture();
        if (!got.Wait(5000)) { Console.Error.WriteLine("wgc-shot: no frame within 5s"); return 4; }
        Thread.Sleep(120); // let one more compose pass land

        // 4. GPU surface → CPU SoftwareBitmap → PNG (all WinRT — no COM byte-access interop)
        var sb = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame!.Surface).GetAwaiter().GetResult();
        int w = sb.PixelWidth, h = sb.PixelHeight;
        var ms = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ms).GetAwaiter().GetResult();
        encoder.SetSoftwareBitmap(sb);
        encoder.FlushAsync().GetAwaiter().GetResult();
        uint size = (uint)ms.Size;
        var reader = new DataReader(ms.GetInputStreamAt(0));
        reader.LoadAsync(size).GetAwaiter().GetResult();
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        File.WriteAllBytes(outPath, bytes);

        Console.WriteLine($"wgc-shot: saved {outPath} ({w}x{h}) from hwnd=0x{hwnd.ToInt64():X}");
        session.Dispose(); framePool.Dispose(); frame!.Dispose();
        return 0;
    }
}
