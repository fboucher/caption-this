using Spectre.Console;

namespace SpectreCaptionThis;

public enum ItemKind { Video, Image }

public class UnifiedItem
{
    public ItemKind Kind { get; init; }
    public string IdOrUrl { get; init; } = string.Empty;
    public string Display { get; init; } = string.Empty;
}
