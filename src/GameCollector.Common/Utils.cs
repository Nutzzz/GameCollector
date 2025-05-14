using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using NexusMods.Paths;

namespace GameCollector.Common;

/// <summary>
/// Utilities.
/// </summary>
public static class Utils
{
    /*
    /// <summary>
    /// Sanitizes the given path.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="PlatformNotSupportedException"></exception>
    public static string SanitizeInputPath(string input)
    {
        var sb = new StringBuilder(input.Trim());

        char currentDirectorySeparator;
        char otherDirectorySeparator;
        if (OperatingSystem.IsLinux())
        {
            currentDirectorySeparator = '/';
            otherDirectorySeparator = '\\';
        }
        else if (OperatingSystem.IsWindows())
        {
            currentDirectorySeparator = '\\';
            otherDirectorySeparator = '/';
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        sb.Replace(otherDirectorySeparator, currentDirectorySeparator);
        sb.Replace(
            $"{currentDirectorySeparator}{currentDirectorySeparator}",
            $"{currentDirectorySeparator}"
        );

        int i;
        for (i = sb.Length - 1; i > 0; i--)
        {
            var c = sb[i];
            if (c != currentDirectorySeparator) break;
        }

        var rootLength = GetRootLength(sb, currentDirectorySeparator);
        if (rootLength == 0 || rootLength != i + 2) return sb.ToString(0, i + 1);
        return sb.ToString(0, rootLength);
    }

    private static int GetRootLength(StringBuilder sb, char directorySeparator)
    {
        if (OperatingSystem.IsLinux())
            return sb.Length >= 1 && sb[0] == directorySeparator ? 1 : 0;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();

        if (sb.Length < 3 || sb[1] != ':' || !IsValidDriveChar(sb[0])) return 0;
        return sb[2] == directorySeparator ? 3 : 0;
    }

    /// <summary>
    /// Returns true if the given character is a valid drive letter.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidDriveChar(char value)
    {
        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
        // source: https://github.com/dotnet/runtime/blob/d9f453924f7c3cca9f02d920a57e1477293f216e/src/libraries/Common/src/System/IO/PathInternal.Windows.cs#L69-L75
        return (uint)((value | 0x20) - 'a') <= 'z' - 'a';
    }
    */

    /// <summary>
    /// Stops a Windows service
    /// </summary>
    /// <param name="name"></param>
    /// <param name="wait"></param>
    [SupportedOSPlatform("windows")]
    public static bool ServiceStop(string name, TimeSpan? wait = null)
    {
        ServiceController sc = new(name);
        try
        {
            if (sc.Status.Equals(ServiceControllerStatus.Running) || sc.Status.Equals(ServiceControllerStatus.StartPending))
            {
                sc.Stop();
                if (wait is not null)
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, (TimeSpan)wait);
                return true;
            }
        }
        catch (Exception) { }

        return false;
    }

    /// <summary>
    /// Starts a Windows service
    /// </summary>
    /// <param name="name"></param>
    /// <param name="wait"></param>
    [SupportedOSPlatform("windows")]
    public static bool ServiceStart(string name, TimeSpan? wait = null)
    {
        ServiceController sc = new(name);
        try
        {
            if (sc.Status.Equals(ServiceControllerStatus.Stopped))
            {
                sc.Start();
                if (wait is not null)
                    sc.WaitForStatus(ServiceControllerStatus.Running, (TimeSpan)wait);
                return true;
            }
        }
        catch (Exception) { }

        return false;
    }

    /// <summary>
    /// Gets status for a Windows service
    /// </summary>
    /// <param name="name"></param>
    [SupportedOSPlatform("windows")]
    public static ServiceControllerStatus ServiceStatus(string name)
    {
        ServiceController sc = new(name);
        try
        {
            return sc.Status;
        }
        catch (Exception) { }

        return default;
    }

    /// <summary>
    /// Tries to convert the string representation of a number to its 16-bit unsigned integer equivalent.
    /// </summary>
    /// <param name="s"></param>
    /// <returns>ushort value if <paramref name="s" /> was converted successfully; otherwise <langword>null</langword>.</returns>
    public static ushort? ToNullableUShort(this string? s)
    {
        if (ushort.TryParse(s, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Tries to convert the string representation of a number to its 32-bit unsigned integer equivalent.
    /// </summary>
    /// <param name="s"></param>
    /// <returns>uint value if <paramref name="s" /> was converted successfully; otherwise <langword>null</langword>.</returns>
    public static uint? ToNullableUInt(this string? s)
    {
        if (uint.TryParse(s, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Tries to convert the string representation of a number to its 64-bit unsigned integer equivalent.
    /// </summary>
    /// <param name="s"></param>
    /// <returns>ulong value if <paramref name="s" /> was converted successfully; otherwise <langword>null</langword>.</returns>
    public static ulong? ToNullableULong(this string? s)
    {
        if (ulong.TryParse(s, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Tries to parse a string into a DateTime value using the CurrentCulture format.
    /// </summary>
    /// <param name="s"></param>
    /// <returns>DateTime value if <paramref name="s" /> was successfully parsed; otherwise <langword>null</langword>.</returns>
    public static DateTime? ToNullableDateTimeCurrent(this string? s)
    {
        if (DateTime.TryParse(s, CultureInfo.CurrentCulture, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Tries to parse a string into a DateTime value using the InvariantCulture format.
    /// </summary>
    /// <param name="s"></param>
    /// <returns>DateTime value if <paramref name="s" /> was successfully parsed; otherwise <langword>null</langword>.</returns>
    public static DateTime? ToNullableDateTimeInvariant(this string? s)
    {
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Returns the path to the best-guess of a game executable found recursively in a given path.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="name"></param>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    public static AbsolutePath FindExe(AbsolutePath path, IFileSystem fileSystem, string name = "")
    {
        try
        {
            AbsolutePath exe = new();
            var exes = fileSystem.EnumerateFiles(path, "*.exe", recursive: true).ToList();

            //TODO: Explore ways of making FindExe() better
            if (exes.Count == 1)
                exe = exes[0];
            else
            {
                for (var i = exes.Count - 1; i >= 0; i--)
                {
                    var bad = false;
                    var good = false;
                    var filename = exes[i].FileName;

                    List<string> badNames =
                    [
                        "unins", "install", "patch", "redist", "prereq", "dotnet", "setup", "config", "w9xpopen", "edit", "help",
                        "python", "server", "service", "cleanup", "anticheat", "touchup", "error", "crash", "report", "handler",
                    ];
                    List<string> goodNames = [];    // ["launch", "scummvm",]

                    foreach (var badName in badNames)
                    {
                        if (filename.Contains(badName, StringComparison.OrdinalIgnoreCase))
                        {
                            bad = true;
                            break;
                        }
                    }
                    foreach (var goodName in goodNames)
                    {
                        if (filename.Contains(goodName, StringComparison.OrdinalIgnoreCase))
                        {
                            good = true;
                            break;
                        }
                    }
                    if (!good && bad)
                        exes.RemoveAt(i);

                    if (!string.IsNullOrEmpty(name))
                    {
                        var nameSanitized = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
                        var nameAlphanum = name.Where(c => c == 32 || (char.IsLetterOrDigit(c) && c < 128)).ToString();
                        if (filename.Contains(nameSanitized, StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(nameSanitized.Replace(' ', '-'), StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(nameSanitized.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(nameSanitized[..' '], StringComparison.OrdinalIgnoreCase) ||
                            (nameAlphanum is not null &&
                            (filename.Contains(nameAlphanum, StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(nameAlphanum.Replace(' ', '-'), StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(nameAlphanum.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase) ||
                            filename.Contains(nameAlphanum[..' '], StringComparison.OrdinalIgnoreCase))))
                        {
                            exe = exes[i];
                            break;
                        }
                    }
                }
            }
            if (exe == default)
                exe = exes.FirstOrDefault();

            return exe;
        }
        catch (Exception) { }

        return default;
    }
}
