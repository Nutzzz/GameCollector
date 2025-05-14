using System.ComponentModel;

namespace GameFinder.Common;

/// <summary>
/// Problem identification
/// </summary>
public enum Problem
{
    /// <summary>
    /// Install pending (queued, downloading, or install in progress)
    /// </summary>
    [Description("This item is waiting to install")]
    InstallPending,
    /// <summary>
    /// Install failure or cancellation
    /// </summary>
    [Description("This item was not installed successfully")]
    InstallFailed,
    /// <summary>
    /// Do not update
    /// </summary>
    [Description("This item will not be updated")]
    VersionLocked,
    /// <summary>
    /// Not found in data (The game is installed, but the launcher may not agree)
    /// </summary>
    /// <remarks>
    /// Opposite of NotFoundInData
    /// </remarks>
    [Description("This item was not found in the launcher's manifests or database")]
    NotFoundInData,
    /// <summary>
    /// Not found on disk (The launcher thinks the game is installed, but we can't find it)
    /// </summary>
    /// <remarks>
    /// Used whenever relying on FindExe() might be necessary.  Opposite of NotFoundOnDisk
    /// </remarks>
    [Description("This item's installation was not found")]
    NotFoundOnDisk,
    /// <summary>
    /// Expired trial
    /// </summary>
    /// <remarks>
    /// Used by BigFish, Humble, and Oculus
    /// </remarks>
    [Description("This item is an expired trial or part of a lapsed membership")]
    ExpiredTrial,
    /// <summary>
    /// Unable to run because of missing prerequisite or the game is not fully suppo by the PC or the emulator
    /// </summary>
    /// <remarks>
    /// The MAME handler uses this for "imperfect" emulation; the Flashpoint handler uses this for "Partial" support
    /// </remarks>
    [Description("This item is not fully working")]
    Incomplete,
    /// <summary>
    /// Unable to run because the game is not supported by the PC or the emulator
    /// </summary>
    /// <remarks>
    /// The MAME handler uses this for "preliminary" drivers; the Flashpoint handler uses this for non-"Playable" entries
    /// </remarks>
    [Description("This item is unplayable")]
    Unplayable,
    /// <summary>
    /// Unable to run because the game is not supported by the PC or the emulator
    /// </summary>
    /// <remarks>
    /// The MAME handler uses this for bootlegs and hacks; the Flashpoint handler uses this for "Hacked" entries
    /// </remarks>
    [Description("This item is a bootleg or hack")]
    Unofficial,
    /// <summary>
    /// Failed to verify (The game is on the disk, but files may be corrupt or a mismatched version)
    /// </summary>
    [Description("This item failed verification")]
    FailedToVerify,
}
