using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameCollector.DataHandlers.TheGamesDB;

internal record Companies
{
    public ushort? Code { get; set; }
    public string? Status { get; set; }
    public CompanyData? Data { get; set; }
    [property: JsonPropertyName("remaining_monthly_allowance")]
    public ushort? RemainingMonthlyAllowance { get; set; }
    [property: JsonPropertyName("extra_allowance")]
    public ushort? ExtraAllowance { get; set; }
    [property: JsonPropertyName("allowance_refresh_timer")]
    public ulong? AllowanceRefreshTimer { get; set; }
    //public Pages? Pages { get; set; }
}

internal record CompanyData
{
    public uint? Count { get; set; }
    public Dictionary<uint, Company>? Developers { get; set; }
    public Dictionary<uint, Company>? Publishers { get; set; }
    public Include? Include { get; set; }
}

internal record Company
{
    public ulong? Id { get; set; }
    public string? Name { get; set; }
}
