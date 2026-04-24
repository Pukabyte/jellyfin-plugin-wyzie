namespace Wyzie.Common;

public sealed class WyzieSearchQuery
{
    public string Id { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? Language { get; set; }
    public string? Format { get; set; }
    public string? Source { get; set; }
    public bool? HearingImpaired { get; set; }
}
