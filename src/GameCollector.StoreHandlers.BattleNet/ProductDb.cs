using System.Collections.Generic;
using ProtoBuf;

namespace GameCollector.StoreHandlers.BattleNet;

// The below is used for reading the Battle.net ProtoBuf database, based on:
// https://github.com/dafzor/bnetlauncher/blob/master/bnetlauncher/Utils/ProductDb.cs
// Copyright (C) 2016-2019 madalien.com

//#pragma warning disable CS1591, CS0612, CS3021, IDE1006, CA1033
[ProtoContract()]
public partial class BnetLanguageSetting : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"language")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Language
    {
        get { return __pbn__Language ?? ""; }
        set { __pbn__Language = value; }
    }
    public bool ShouldSerializeLanguage() => __pbn__Language != null;
    public void ResetLanguage() => __pbn__Language = null;
    private string? __pbn__Language;

    [ProtoMember(2, Name = @"option")]
    [global::System.ComponentModel.DefaultValue(BnetLanguageOption.LangoptionNone)]
    public BnetLanguageOption Option
    {
        get { return __pbn__Option ?? BnetLanguageOption.LangoptionNone; }
        set { __pbn__Option = value; }
    }
    public bool ShouldSerializeOption() => __pbn__Option != null;
    public void ResetOption() => __pbn__Option = null;
    private BnetLanguageOption? __pbn__Option;

}

[ProtoContract()]
public partial class BnetUserSettings : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    [global::System.ComponentModel.DefaultValue("")]
    public string installPath
    {
        get { return __pbn__installPath ?? ""; }
        set { __pbn__installPath = value; }
    }
    public bool ShouldSerializeinstallPath() => __pbn__installPath != null;
    public void ResetinstallPath() => __pbn__installPath = null;
    private string? __pbn__installPath;

    [ProtoMember(2)]
    [global::System.ComponentModel.DefaultValue("")]
    public string playRegion
    {
        get { return __pbn__playRegion ?? ""; }
        set { __pbn__playRegion = value; }
    }
    public bool ShouldSerializeplayRegion() => __pbn__playRegion != null;
    public void ResetplayRegion() => __pbn__playRegion = null;
    private string? __pbn__playRegion;

    [ProtoMember(3)]
    [global::System.ComponentModel.DefaultValue(BnetShortcutOption.ShortcutNone)]
    public BnetShortcutOption desktopShortcut
    {
        get { return __pbn__desktopShortcut ?? BnetShortcutOption.ShortcutNone; }
        set { __pbn__desktopShortcut = value; }
    }
    public bool ShouldSerializedesktopShortcut() => __pbn__desktopShortcut != null;
    public void ResetdesktopShortcut() => __pbn__desktopShortcut = null;
    private BnetShortcutOption? __pbn__desktopShortcut;

    [ProtoMember(4)]
    [global::System.ComponentModel.DefaultValue(BnetShortcutOption.ShortcutNone)]
    public BnetShortcutOption startmenuShortcut
    {
        get { return __pbn__startmenuShortcut ?? BnetShortcutOption.ShortcutNone; }
        set { __pbn__startmenuShortcut = value; }
    }
    public bool ShouldSerializestartmenuShortcut() => __pbn__startmenuShortcut != null;
    public void ResetstartmenuShortcut() => __pbn__startmenuShortcut = null;
    private BnetShortcutOption? __pbn__startmenuShortcut;

    [ProtoMember(5)]
    [global::System.ComponentModel.DefaultValue(BnetLanguageSettingType.LangsettingNone)]
    public BnetLanguageSettingType languageSettings
    {
        get { return __pbn__languageSettings ?? BnetLanguageSettingType.LangsettingNone; }
        set { __pbn__languageSettings = value; }
    }
    public bool ShouldSerializelanguageSettings() => __pbn__languageSettings != null;
    public void ResetlanguageSettings() => __pbn__languageSettings = null;
    private BnetLanguageSettingType? __pbn__languageSettings;

    [ProtoMember(6)]
    [global::System.ComponentModel.DefaultValue("")]
    public string selectedTextLanguage
    {
        get { return __pbn__selectedTextLanguage ?? ""; }
        set { __pbn__selectedTextLanguage = value; }
    }
    public bool ShouldSerializeselectedTextLanguage() => __pbn__selectedTextLanguage != null;
    public void ResetselectedTextLanguage() => __pbn__selectedTextLanguage = null;
    private string? __pbn__selectedTextLanguage;

    [ProtoMember(7)]
    [global::System.ComponentModel.DefaultValue("")]
    public string selectedSpeechLanguage
    {
        get { return __pbn__selectedSpeechLanguage ?? ""; }
        set { __pbn__selectedSpeechLanguage = value; }
    }
    public bool ShouldSerializeselectedSpeechLanguage() => __pbn__selectedSpeechLanguage != null;
    public void ResetselectedSpeechLanguage() => __pbn__selectedSpeechLanguage = null;
    private string? __pbn__selectedSpeechLanguage;

    [ProtoMember(8, Name = @"languages")]
    public IList<BnetLanguageSetting> Languages { get; } = [];

    [ProtoMember(9, Name = @"gfx_override_tags")]
    [global::System.ComponentModel.DefaultValue("")]
    public string GfxOverrideTags
    {
        get { return __pbn__GfxOverrideTags ?? ""; }
        set { __pbn__GfxOverrideTags = value; }
    }
    public bool ShouldSerializeGfxOverrideTags() => __pbn__GfxOverrideTags != null;
    public void ResetGfxOverrideTags() => __pbn__GfxOverrideTags = null;
    private string? __pbn__GfxOverrideTags;

    [ProtoMember(10, Name = @"versionbranch")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Versionbranch
    {
        get { return __pbn__Versionbranch ?? ""; }
        set { __pbn__Versionbranch = value; }
    }
    public bool ShouldSerializeVersionbranch() => __pbn__Versionbranch != null;
    public void ResetVersionbranch() => __pbn__Versionbranch = null;
    private string? __pbn__Versionbranch;

}

[ProtoContract()]
public partial class BnetInstallHandshake : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"product")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Product
    {
        get { return __pbn__Product ?? ""; }
        set { __pbn__Product = value; }
    }
    public bool ShouldSerializeProduct() => __pbn__Product != null;
    public void ResetProduct() => __pbn__Product = null;
    private string? __pbn__Product;

    [ProtoMember(2, Name = @"uid")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Uid
    {
        get { return __pbn__Uid ?? ""; }
        set { __pbn__Uid = value; }
    }
    public bool ShouldSerializeUid() => __pbn__Uid != null;
    public void ResetUid() => __pbn__Uid = null;
    private string? __pbn__Uid;

    [ProtoMember(3, Name = @"settings")]
    public BnetUserSettings? Settings { get; set; }

}

[ProtoContract()]
public partial class BnetBuildConfig : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"region")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Region
    {
        get { return __pbn__Region ?? ""; }
        set { __pbn__Region = value; }
    }
    public bool ShouldSerializeRegion() => __pbn__Region != null;
    public void ResetRegion() => __pbn__Region = null;
    private string? __pbn__Region;

    [ProtoMember(2)]
    [global::System.ComponentModel.DefaultValue("")]
    public string buildConfig
    {
        get { return __pbn__buildConfig ?? ""; }
        set { __pbn__buildConfig = value; }
    }
    public bool ShouldSerializebuildConfig() => __pbn__buildConfig != null;
    public void ResetbuildConfig() => __pbn__buildConfig = null;
    private string? __pbn__buildConfig;

}

[ProtoContract()]
public partial class BnetBaseProductState : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"installed")]
    public bool Installed
    {
        get { return __pbn__Installed.GetValueOrDefault(); }
        set { __pbn__Installed = value; }
    }
    public bool ShouldSerializeInstalled() => __pbn__Installed != null;
    public void ResetInstalled() => __pbn__Installed = null;
    private bool? __pbn__Installed;

    [ProtoMember(2, Name = @"playable")]
    public bool Playable
    {
        get { return __pbn__Playable.GetValueOrDefault(); }
        set { __pbn__Playable = value; }
    }
    public bool ShouldSerializePlayable() => __pbn__Playable != null;
    public void ResetPlayable() => __pbn__Playable = null;
    private bool? __pbn__Playable;

    [ProtoMember(3)]
    public bool updateComplete
    {
        get { return __pbn__updateComplete.GetValueOrDefault(); }
        set { __pbn__updateComplete = value; }
    }
    public bool ShouldSerializeupdateComplete() => __pbn__updateComplete != null;
    public void ResetupdateComplete() => __pbn__updateComplete = null;
    private bool? __pbn__updateComplete;

    [ProtoMember(4)]
    public bool backgroundDownloadAvailable
    {
        get { return __pbn__backgroundDownloadAvailable.GetValueOrDefault(); }
        set { __pbn__backgroundDownloadAvailable = value; }
    }
    public bool ShouldSerializebackgroundDownloadAvailable() => __pbn__backgroundDownloadAvailable != null;
    public void ResetbackgroundDownloadAvailable() => __pbn__backgroundDownloadAvailable = null;
    private bool? __pbn__backgroundDownloadAvailable;

    [ProtoMember(5)]
    public bool backgroundDownloadComplete
    {
        get { return __pbn__backgroundDownloadComplete.GetValueOrDefault(); }
        set { __pbn__backgroundDownloadComplete = value; }
    }
    public bool ShouldSerializebackgroundDownloadComplete() => __pbn__backgroundDownloadComplete != null;
    public void ResetbackgroundDownloadComplete() => __pbn__backgroundDownloadComplete = null;
    private bool? __pbn__backgroundDownloadComplete;

    [ProtoMember(6)]
    [global::System.ComponentModel.DefaultValue("")]
    public string currentVersion
    {
        get { return __pbn__currentVersion ?? ""; }
        set { __pbn__currentVersion = value; }
    }
    public bool ShouldSerializecurrentVersion() => __pbn__currentVersion != null;
    public void ResetcurrentVersion() => __pbn__currentVersion = null;
    private string? __pbn__currentVersion;

    [ProtoMember(7)]
    [global::System.ComponentModel.DefaultValue("")]
    public string currentVersionStr
    {
        get { return __pbn__currentVersionStr ?? ""; }
        set { __pbn__currentVersionStr = value; }
    }
    public bool ShouldSerializecurrentVersionStr() => __pbn__currentVersionStr != null;
    public void ResetcurrentVersionStr() => __pbn__currentVersionStr = null;
    private string? __pbn__currentVersionStr;

    [ProtoMember(8, Name = @"installedBuildConfig")]
    public IList<BnetBuildConfig> installedBuildConfigs { get; } = [];

    [ProtoMember(9, Name = @"backgroundDownloadBuildConfig")]
    public IList<BnetBuildConfig> backgroundDownloadBuildConfigs { get; } = [];

    [ProtoMember(10)]
    [global::System.ComponentModel.DefaultValue("")]
    public string decryptionKey
    {
        get { return __pbn__decryptionKey ?? ""; }
        set { __pbn__decryptionKey = value; }
    }
    public bool ShouldSerializedecryptionKey() => __pbn__decryptionKey != null;
    public void ResetdecryptionKey() => __pbn__decryptionKey = null;
    private string? __pbn__decryptionKey;

    [ProtoMember(11)]
    public IList<string> completedInstallActions { get; } = [];

}

[ProtoContract()]
public partial class BnetBackfillProgress : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"progress")]
    public double Progress
    {
        get { return __pbn__Progress.GetValueOrDefault(); }
        set { __pbn__Progress = value; }
    }
    public bool ShouldSerializeProgress() => __pbn__Progress != null;
    public void ResetProgress() => __pbn__Progress = null;
    private double? __pbn__Progress;

    [ProtoMember(2, Name = @"backgrounddownload")]
    public bool Backgrounddownload
    {
        get { return __pbn__Backgrounddownload.GetValueOrDefault(); }
        set { __pbn__Backgrounddownload = value; }
    }
    public bool ShouldSerializeBackgrounddownload() => __pbn__Backgrounddownload != null;
    public void ResetBackgrounddownload() => __pbn__Backgrounddownload = null;
    private bool? __pbn__Backgrounddownload;

    [ProtoMember(3, Name = @"paused")]
    public bool Paused
    {
        get { return __pbn__Paused.GetValueOrDefault(); }
        set { __pbn__Paused = value; }
    }
    public bool ShouldSerializePaused() => __pbn__Paused != null;
    public void ResetPaused() => __pbn__Paused = null;
    private bool? __pbn__Paused;

    [ProtoMember(4)]
    public ulong downloadLimit
    {
        get { return __pbn__downloadLimit.GetValueOrDefault(); }
        set { __pbn__downloadLimit = value; }
    }
    public bool ShouldSerializedownloadLimit() => __pbn__downloadLimit != null;
    public void ResetdownloadLimit() => __pbn__downloadLimit = null;
    private ulong? __pbn__downloadLimit;

}

[ProtoContract()]
public partial class BnetRepairProgress : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"progress")]
    public double Progress
    {
        get { return __pbn__Progress.GetValueOrDefault(); }
        set { __pbn__Progress = value; }
    }
    public bool ShouldSerializeProgress() => __pbn__Progress != null;
    public void ResetProgress() => __pbn__Progress = null;
    private double? __pbn__Progress;

}

[ProtoContract()]
public partial class BnetUpdateProgress : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    [global::System.ComponentModel.DefaultValue("")]
    public string lastDiscSetUsed
    {
        get { return __pbn__lastDiscSetUsed ?? ""; }
        set { __pbn__lastDiscSetUsed = value; }
    }
    public bool ShouldSerializelastDiscSetUsed() => __pbn__lastDiscSetUsed != null;
    public void ResetlastDiscSetUsed() => __pbn__lastDiscSetUsed = null;
    private string? __pbn__lastDiscSetUsed;

    [ProtoMember(2, Name = @"progress")]
    public double Progress
    {
        get { return __pbn__Progress.GetValueOrDefault(); }
        set { __pbn__Progress = value; }
    }
    public bool ShouldSerializeProgress() => __pbn__Progress != null;
    public void ResetProgress() => __pbn__Progress = null;
    private double? __pbn__Progress;

    [ProtoMember(3)]
    public bool discIgnored
    {
        get { return __pbn__discIgnored.GetValueOrDefault(); }
        set { __pbn__discIgnored = value; }
    }
    public bool ShouldSerializediscIgnored() => __pbn__discIgnored != null;
    public void ResetdiscIgnored() => __pbn__discIgnored = null;
    private bool? __pbn__discIgnored;

    [ProtoMember(4)]
    [global::System.ComponentModel.DefaultValue(0)]
    public ulong totalToDownload
    {
        get { return __pbn__totalToDownload ?? 0; }
        set { __pbn__totalToDownload = value; }
    }
    public bool ShouldSerializetotalToDownload() => __pbn__totalToDownload != null;
    public void ResettotalToDownload() => __pbn__totalToDownload = null;
    private ulong? __pbn__totalToDownload;

    [ProtoMember(5)]
    [global::System.ComponentModel.DefaultValue(0)]
    public ulong downloadRemaining
    {
        get { return __pbn__downloadRemaining ?? 0; }
        set { __pbn__downloadRemaining = value; }
    }
    public bool ShouldSerializedownloadRemaining() => __pbn__downloadRemaining != null;
    public void ResetdownloadRemaining() => __pbn__downloadRemaining = null;
    private ulong? __pbn__downloadRemaining;

}

[ProtoContract()]
public partial class BnetCachedProductState : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    public BnetBaseProductState? baseProductState { get; set; }

    [ProtoMember(2)]
    public BnetBackfillProgress? backfillProgress { get; set; }

    [ProtoMember(3)]
    public BnetRepairProgress? repairProgress { get; set; }

    [ProtoMember(4)]
    public BnetUpdateProgress? updateProgress { get; set; }

}

[ProtoContract()]
public partial class BnetProductOperations : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    [global::System.ComponentModel.DefaultValue(BnetOperation.OpNone)]
    public BnetOperation activeOperation
    {
        get { return __pbn__activeOperation ?? BnetOperation.OpNone; }
        set { __pbn__activeOperation = value; }
    }
    public bool ShouldSerializeactiveOperation() => __pbn__activeOperation != null;
    public void ResetactiveOperation() => __pbn__activeOperation = null;
    private BnetOperation? __pbn__activeOperation;

    [ProtoMember(2, Name = @"priority")]
    public ulong Priority
    {
        get { return __pbn__Priority.GetValueOrDefault(); }
        set { __pbn__Priority = value; }
    }
    public bool ShouldSerializePriority() => __pbn__Priority != null;
    public void ResetPriority() => __pbn__Priority = null;
    private ulong? __pbn__Priority;

}

[ProtoContract()]
public partial class BnetProductInstall : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"uid")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Uid
    {
        get { return __pbn__Uid ?? ""; }
        set { __pbn__Uid = value; }
    }
    public bool ShouldSerializeUid() => __pbn__Uid != null;
    public void ResetUid() => __pbn__Uid = null;
    private string? __pbn__Uid;

    [ProtoMember(2)]
    [global::System.ComponentModel.DefaultValue("")]
    public string productCode
    {
        get { return __pbn__productCode ?? ""; }
        set { __pbn__productCode = value; }
    }
    public bool ShouldSerializeproductCode() => __pbn__productCode != null;
    public void ResetproductCode() => __pbn__productCode = null;
    private string? __pbn__productCode;

    [ProtoMember(3, Name = @"settings")]
    public BnetUserSettings? Settings { get; set; }

    [ProtoMember(4)]
    public BnetCachedProductState? cachedProductState { get; set; }

    [ProtoMember(5)]
    public BnetProductOperations? productOperations { get; set; }

}

[ProtoContract()]
public partial class BnetProductConfig : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    [global::System.ComponentModel.DefaultValue("")]
    public string productCode
    {
        get { return __pbn__productCode ?? ""; }
        set { __pbn__productCode = value; }
    }
    public bool ShouldSerializeproductCode() => __pbn__productCode != null;
    public void ResetproductCode() => __pbn__productCode = null;
    private string? __pbn__productCode;

    [ProtoMember(2)]
    [global::System.ComponentModel.DefaultValue("")]
    public string metadataHash
    {
        get { return __pbn__metadataHash ?? ""; }
        set { __pbn__metadataHash = value; }
    }
    public bool ShouldSerializemetadataHash() => __pbn__metadataHash != null;
    public void ResetmetadataHash() => __pbn__metadataHash = null;
    private string? __pbn__metadataHash;

    [ProtoMember(3, Name = @"timestamp")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Timestamp
    {
        get { return __pbn__Timestamp ?? ""; }
        set { __pbn__Timestamp = value; }
    }
    public bool ShouldSerializeTimestamp() => __pbn__Timestamp != null;
    public void ResetTimestamp() => __pbn__Timestamp = null;
    private string? __pbn__Timestamp;

}

[ProtoContract()]
public partial class BnetActiveProcess : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    [global::System.ComponentModel.DefaultValue("")]
    public string processName
    {
        get { return __pbn__processName ?? ""; }
        set { __pbn__processName = value; }
    }
    public bool ShouldSerializeprocessName() => __pbn__processName != null;
    public void ResetprocessName() => __pbn__processName = null;
    private string? __pbn__processName;

    [ProtoMember(2, Name = @"pid")]
    public int Pid
    {
        get { return __pbn__Pid.GetValueOrDefault(); }
        set { __pbn__Pid = value; }
    }
    public bool ShouldSerializePid() => __pbn__Pid != null;
    public void ResetPid() => __pbn__Pid = null;
    private int? __pbn__Pid;

    [ProtoMember(3, Name = @"uri")]
    public IList<string> Uris { get; } = [];

}

[ProtoContract()]
public partial class BnetDownloadSettings : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1)]
    [global::System.ComponentModel.DefaultValue(-1)]
    public int downloadLimit
    {
        get { return __pbn__downloadLimit ?? -1; }
        set { __pbn__downloadLimit = value; }
    }
    public bool ShouldSerializedownloadLimit() => __pbn__downloadLimit != null;
    public void ResetdownloadLimit() => __pbn__downloadLimit = null;
    private int? __pbn__downloadLimit;

    [ProtoMember(2)]
    [global::System.ComponentModel.DefaultValue(-1)]
    public int backfillLimit
    {
        get { return __pbn__backfillLimit ?? -1; }
        set { __pbn__backfillLimit = value; }
    }
    public bool ShouldSerializebackfillLimit() => __pbn__backfillLimit != null;
    public void ResetbackfillLimit() => __pbn__backfillLimit = null;
    private int? __pbn__backfillLimit;

}

[ProtoContract()]
public partial class BnetDatabase : IExtensible
{
    private IExtension? __pbn__extensionData;
    IExtension IExtensible.GetExtensionObject(bool createIfMissing)
        => Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [ProtoMember(1, Name = @"productInstall")]
    public IList<BnetProductInstall> productInstalls { get; } = [];

    [ProtoMember(2)]
    public IList<BnetInstallHandshake> activeInstalls { get; } = [];

    [ProtoMember(3)]
    public IList<BnetActiveProcess> activeProcesses { get; } = [];

    [ProtoMember(4)]
    public IList<BnetProductConfig> productConfigs { get; } = [];

    [ProtoMember(5)]
    public BnetDownloadSettings? downloadSettings { get; set; }

}

[ProtoContract()]
public enum BnetLanguageOption
{
    [ProtoEnum(Name = @"LANGOPTION_NONE")]
    LangoptionNone = 0,
    [ProtoEnum(Name = @"LANGOPTION_TEXT")]
    LangoptionText = 1,
    [ProtoEnum(Name = @"LANGOPTION_SPEECH")]
    LangoptionSpeech = 2,
    [ProtoEnum(Name = @"LANGOPTION_TEXT_AND_SPEECH")]
    LangoptionTextAndSpeech = 3,
}

[ProtoContract()]
public enum BnetLanguageSettingType
{
    [ProtoEnum(Name = @"LANGSETTING_NONE")]
    LangsettingNone = 0,
    [ProtoEnum(Name = @"LANGSETTING_SINGLE")]
    LangsettingSingle = 1,
    [ProtoEnum(Name = @"LANGSETTING_SIMPLE")]
    LangsettingSimple = 2,
    [ProtoEnum(Name = @"LANGSETTING_ADVANCED")]
    LangsettingAdvanced = 3,
}

[ProtoContract()]
public enum BnetShortcutOption
{
    [ProtoEnum(Name = @"SHORTCUT_NONE")]
    ShortcutNone = 0,
    [ProtoEnum(Name = @"SHORTCUT_USER")]
    ShortcutUser = 1,
    [ProtoEnum(Name = @"SHORTCUT_ALL_USERS")]
    ShortcutAllUsers = 2,
}

[ProtoContract()]
public enum BnetOperation
{
    [ProtoEnum(Name = @"OP_NONE")]
    OpNone = -1,
    [ProtoEnum(Name = @"OP_UPDATE")]
    OpUpdate = 0,
    [ProtoEnum(Name = @"OP_BACKFILL")]
    OpBackfill = 1,
    [ProtoEnum(Name = @"OP_REPAIR")]
    OpRepair = 2,
}
//#pragma warning restore CS1591, CS0612, CS3021, IDE1006, CA1033
