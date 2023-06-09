using System.Xml.Serialization;
using JetBrains.Annotations;
#pragma warning disable CS1591

namespace GameFinder.StoreHandlers.Xbox;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[XmlRoot(ElementName = "Identity", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
public class Identity
{
    [XmlAttribute(AttributeName = "Name", Namespace = "")]
    public string Name { get; set; } = null!;
    [XmlAttribute(AttributeName = "Publisher", Namespace = "")]
    public string Publisher { get; set; } = null!;
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[XmlRoot(ElementName = "Properties", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
public class Properties
{
    [XmlElement(ElementName = "DisplayName", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
    public string DisplayName { get; set; } = null!;
    [XmlElement(ElementName = "PublisherDisplayName", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
    public string PublisherDisplayName { get; set; } = null!;
    [XmlElement(ElementName = "Description", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
    public string Description { get; set; } = null!;
    [XmlElement(ElementName = "Logo", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
    public string Logo { get; set; } = null!;
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[XmlRoot(ElementName = "Package", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
public class Package
{
    [XmlElement(ElementName = "Identity", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
    public Identity Identity { get; set; } = null!;

    [XmlElement(ElementName = "Properties", Namespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10")]
    public Properties Properties { get; set; } = null!;
}
