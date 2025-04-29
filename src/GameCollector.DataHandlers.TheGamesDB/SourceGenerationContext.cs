using System.Text.Json.Serialization;

namespace GameCollector.DataHandlers.TheGamesDB;

[JsonSourceGenerationOptions(WriteIndented = false, GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Companies))]
[JsonSerializable(typeof(Database))]
internal partial class SourceGenerationContext : JsonSerializerContext { }
