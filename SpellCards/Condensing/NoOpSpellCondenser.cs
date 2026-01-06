using SpellCards.Models;

namespace SpellCards.Condensing;

public sealed class NoOpSpellCondenser : ISpellCondenser
{
    public Task<string> CondenseAsync(Spell spell, CancellationToken ct)
        => Task.FromResult(spell.Description);
}
