using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace D3dxSkinManager.ScreenCapture
{
    /// <summary>
    /// Plugin for capturing screenshots of the application window
    /// </summary>
    public class ScreenCapturePlugin : IMessageHandlerPlugin
    {
        private IPluginContext _context = null!;
        private string _screenshotDir = string.Empty;

        public string Id => "com.d3dxskinmanager.screencapture";
        public string Name => "Screen Capture";
        public string Version => "1.0.0";
        public string Description => "Captures screenshots of the application window";
        public string Author => "D3dxSkinManager Team";

        public IEnumerable<string> GetHandledMessageTypes() => new[]
        {
            "CAPTURE_SCREENSHOT",
            "GET_LAST_SCREENSHOT",
            "LIST_SCREENSHOTS"
        };

        public Task InitializeAsync(IPluginContext context)
        {
            _context = context;
            _screenshotDir = Path.Combine(_context.GetPluginDataPath(Id), "screenshots");
            Directory.CreateDirectory(_screenshotDir);
            _context.Log(LogLevel.Info, $"{Name} initialized. Screenshots saved to: {_screenshotDir}");
            return Task.CompletedTask;
        }

        public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
        {
            try
            {
                return request.Type switch
                {
                    "CAPTURE_SCREENSHOT" => await CaptureScreenshotAsync(request),
                    "GET_LAST_SCREENSHOT" => await GetLastScreenshotAsync(request),
                    "LIST_SCREENSHOTS" => await ListScreenshotsAsync(request),
                    _ => new MessageResponse { Id = request.Id, Success = false, Error = "Unknown message type" }
                };
            }
            catch (Exception ex)
            {
                _context.Log(LogLevel.Error, $"Error in {Name}", ex);
                return new MessageResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private Task<MessageResponse> CaptureScreenshotAsync(MessageRequest request)
        {
            try
            {
                var screenshot = CaptureWindow();
                if (screenshot == null)
                {
                    return Task.FromResult(new MessageResponse
                    {
                        Id = request.Id,
                        Success = false,
                        Error = "Failed to capture screenshot"
                    });
                }

                // Save screenshot
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"screenshot_{timestamp}.png";
                var filepath = Path.Combine(_screenshotDir, filename);

                screenshot.Save(filepath, ImageFormat.Png);
                screenshot.Dispose();

                // Convert to base64 for returning to frontend
                var base64Image = ConvertImageToBase64(filepath);

                _context.Log(LogLevel.Info, $"Screenshot saved: {filename}");

                return Task.FromResult(new MessageResponse
                {
                    Id = request.Id,
                    Success = true,
                    Data = new
                    {
                        filename,
                        filepath,
                        base64 = base64Image,
                        timestamp
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new MessageResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private Task<MessageResponse> GetLastScreenshotAsync(MessageRequest request)
        {
            try
            {
                var files = Directory.GetFiles(_screenshotDir, "screenshot_*.png");
                if (files.Length == 0)
                {
                    return Task.FromResult(new MessageResponse
                    {
                        Id = request.Id,
                        Success = false,
                        Error = "No screenshots found"
                    });
                }

                // Get most recent
                Array.Sort(files);
                var latestFile = files[^1];
                var base64Image = ConvertImageToBase64(latestFile);

                return Task.FromResult(new MessageResponse
                {
                    Id = request.Id,
                    Success = true,
                    Data = new
                    {
                        filename = Path.GetFileName(latestFile),
                        filepath = latestFile,
                        base64 = base64Image
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new MessageResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private Task<MessageResponse> ListScreenshotsAsync(MessageRequest request)
        {
            try
            {
                var files = Directory.GetFiles(_screenshotDir, "screenshot_*.png");
                var screenshots = new List<object>();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    screenshots.Add(new
                    {
                        filename = fileInfo.Name,
                        filepath = fileInfo.FullName,
                        size = fileInfo.Length,
                        created = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                return Task.FromResult(new MessageResponse
                {
                    Id = request.Id,
                    Success = true,
                    Data = new
                    {
                        screenshots,
                        count = screenshots.Count,
                        directory = _screenshotDir
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new MessageResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private Bitmap? CaptureWindow()
        {
            try
            {
                // Get the active window
                var handle = GetForegroundWindow();
                if (handle == IntPtr.Zero)
                    return null;

                // Get window dimensions
                if (!GetWindowRect(handle, out RECT rect))
                    return null;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0)
                    return null;

                // Create bitmap
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var hdcBitmap = graphics.GetHdc();
                    try
                    {
                        // Print window to bitmap
                        PrintWindow(handle, hdcBitmap, 0);
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdcBitmap);
                    }
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                _context.Log(LogLevel.Error, "Error capturing window", ex);
                return null;
            }
        }

        private string ConvertImageToBase64(string filepath)
        {
            byte[] imageBytes = File.ReadAllBytes(filepath);
            return Convert.ToBase64String(imageBytes);
        }

        public Task ShutdownAsync()
        {
            _context.Log(LogLevel.Info, $"{Name} shutting down");
            return Task.CompletedTask;
        }

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
