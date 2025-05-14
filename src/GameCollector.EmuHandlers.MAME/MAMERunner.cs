using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NexusMods.Paths;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace GameCollector.EmuHandlers.MAME;

// Originally based on https://github.com/mika76/mamesaver
// Copyright (c) 2007 Contributors

/// <summary>
///     Invokes the MAME executable.
/// </summary>
public class MAMERunner
{
    private static readonly ILogger logger = new NLogLoggerProvider().CreateLogger("MAME");
    private readonly List<Process> _processes = [];

    /// <summary>
    ///     Kills a MAME process.
    /// </summary>
    public static void Stop(Process process)
    {
        if (process is null || process.HasExited) return;

        logger.LogDebug("Stopping MAME; pid: {pid}", process.Id);

        try
        {
            // Minimize and then exit. Minimising it makes it disappear instantly.
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                WindowsInterop.MinimizeWindow(process.MainWindowHandle);
                process.CloseMainWindow();

                logger.LogDebug("Waiting for MAME to exit");
                if (!process.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds))
                {
                    logger.LogWarning("Timeout waiting for MAME to exit; killing MAME");
                    process.Kill();
                }
            }
            else
            {
                logger.LogDebug("Killing MAME as no window handle");
                process.Kill();
            }

            process.WaitForExit();
            logger.LogDebug("MAME stopped; pid {pid}", process.Id);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error stopping MAME");
        }
    }

    /// <summary>
    ///     Invokes MAME, returning the created process.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="exePath">path to MAME executable</param>
    /// <param name="arguments">arguments to pass to MAME</param>
    public static Process Run(IFileSystem fileSystem, AbsolutePath exePath, params string?[] arguments)
    {
        //logger.LogDebug("Invoking MAME with arguments: {arguments}", string.Join(' ', arguments));

        if (!fileSystem.FileExists(exePath))
        {
            logger.LogError("MAME path not found: {path}", exePath);
            return new();
        }

        var workingDir = exePath.Directory;
        var psi = new ProcessStartInfo(exePath.GetFullPath())
        {
            Arguments = string.Join(' ', arguments),
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                logger.LogError("MAME process not started: {filename} {arguments},", psi.FileName, psi.Arguments);
                return new();
            }
            //logger.LogDebug("MAME started; pid: {pid}", process.Id);

            process.EnableRaisingEvents = true;

            // Register process so we can terminate all processes when the container is disposed
            //Register(process);

            return process;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unable to run MAME: {filename} {arguments}", psi.FileName, psi.Arguments);
            return new();
        }
    }

    /// <summary>
    ///     Registers a MAME process for automatic termination on shutdown.
    /// </summary>
    /// <param name="process"></param>
    public void Register(Process process) => _processes.Add(process);

    public virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        TryStopProcesses();
    }

    /// <summary>
    ///     Tries to stop all registered MAME processes.
    /// </summary>
    private void TryStopProcesses()
    {
        _processes.ForEach(process =>
        {
            try
            {
                // Stop MAME and wait for it to terminate
                Stop(process);
            }
            catch (InvalidOperationException)
            {
                logger.LogWarning("Unable to stop MAME; it may not have fully started.");
            }
        });
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    //~MAMERunner() => Dispose(disposing: false);
}

public static class WindowsInterop
{
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private const int SwpShowwindow = 64; // 0×0040

    public static void SetWinFullScreen(IntPtr hwnd, int left, int top, int width, int height)
    {
        PlatformInvokeUser32.SetWindowPos(hwnd, HwndTop, left, top, width, height, SwpShowwindow);
    }

    public static void MinimizeWindow(IntPtr hwnd)
    {
        PlatformInvokeUser32.ShowWindow(hwnd, PlatformInvokeUser32.SW_MINIMIZE);
    }

    public static void SetHighDpiAware()
    {
        if (Environment.OSVersion.Version.Major >= 6)
            PlatformInvokeUser32.SetProcessDPIAware();
    }
}

public class PlatformInvokeUser32
{
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const int SW_MINIMIZE = 6;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern void SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
    internal static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", EntryPoint = "GetDC")]
    internal static extern IntPtr GetDC(IntPtr ptr);

    [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    internal static extern int GetSystemMetrics(int abc);

    [DllImport("user32.dll", EntryPoint = "GetWindowDC")]
    internal static extern IntPtr GetWindowDC(int ptr);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
    internal static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner
    }

    [DllImport("user32.dll")]
    internal static extern bool SetProcessDPIAware();
}
