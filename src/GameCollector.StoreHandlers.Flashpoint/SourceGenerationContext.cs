using System.Text.Json.Serialization;

namespace GameCollector.StoreHandlers.Flashpoint;

[JsonSourceGenerationOptions(WriteIndented = false, GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Preferences))]
internal partial class SourceGenerationContext : JsonSerializerContext { }
