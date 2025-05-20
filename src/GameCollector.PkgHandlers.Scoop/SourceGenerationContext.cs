using System.Text.Json.Serialization;

namespace GameCollector.PkgHandlers.Scoop;

[JsonSourceGenerationOptions(WriteIndented = false, GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Export))]
internal partial class SourceGenerationContext : JsonSerializerContext { }
