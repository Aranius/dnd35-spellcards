using SpellCards.Models;

namespace SpellCards.Condensing;

public interface ISpellCondenser
{
    Task<string> CondenseAsync(Spell spell, CancellationToken ct);
}
