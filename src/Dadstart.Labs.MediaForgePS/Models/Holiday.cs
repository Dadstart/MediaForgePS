namespace Dadstart.Labs.MediaForgePS.Models;

/// <summary>
/// Represents a holiday with details from timeanddate.com.
/// </summary>
public record Holiday
{
    /// <summary>
    /// Date of the holiday.
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Name of the holiday.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Type of holiday (e.g., "Federal Holiday", "State Holiday", "Observance").
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Description of the holiday.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Observances or additional information about the holiday.
    /// </summary>
    public string Observances { get; init; } = string.Empty;
}


