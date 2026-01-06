namespace SpellCards.Models;

public sealed record Spell
{
    public required string Name { get; init; }
    public string Part { get; init; } = "";               // "1/2" etc.
    public required string ClassLevel { get; init; }      // "Wizard 1"
    public required string SchoolText { get; init; }      // "Evocation [Fire]"
    public required string SchoolKey { get; init; }       // "evocation"

    public required string Cast { get; init; }
    public required string Range { get; init; }
    public required string TargetOrArea { get; init; }    // "Target: ..." or "Area: ..."
    public required string Duration { get; init; }
    public required string Save { get; init; }            // "None" allowed
    public required string Sr { get; init; }              // "Yes"/"No"
    public required string Components { get; init; }      // "V S M"
    public string Tags { get; init; } = "";               // optional
    public required string Description { get; init; }
    public string Notes { get; init; } = "";
    public required string SourceUrl { get; init; }
}
