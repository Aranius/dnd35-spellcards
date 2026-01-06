using SpellCards.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SpellCards.Rendering;

public sealed class SpellCardDocument : IDocument
{
    private readonly IReadOnlyList<Spell> _cards;

    // Page & grid configuration
    private const int Columns = 3;
    private const int Rows = 3;

    private const float CardWidthMm = 63f;
    private const float CardHeightMm = 88f;
    private const float GapMm = 0f;

    private const float PageWidthMm = 210f;
    private const float PageHeightMm = 297f;
    private const float PageMarginMm = 5f;

    public SpellCardDocument(IEnumerable<Spell> spells)
    {
        // ✅ Split ONCE, render ONLY cards
        _cards = spells
            .SelectMany(SpellSplitter.SplitIfNeeded)
            .ToList();
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(PageMarginMm, Unit.Millimetre);
            page.PageColor(Colors.White);

            page.Content().Element(ComposeAllPages);
        });
    }

    private void ComposeAllPages(IContainer container)
    {
        int cardsPerPage = Columns * Rows;
        int pageCount = (int)Math.Ceiling(_cards.Count / (double)cardsPerPage);

        container.Column(col =>
        {
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var pageCards = _cards
                    .Skip(pageIndex * cardsPerPage)
                    .Take(cardsPerPage)
                    .ToList();

                col.Item()
                   .Element(c => ComposeSinglePage(c, pageCards));

                if (pageIndex < pageCount - 1)
                    col.Item().PageBreak();
            }
        });
    }

    private void ComposeSinglePage(IContainer container, IReadOnlyList<Spell> cards)
    {
        float gridWidth = Columns * CardWidthMm + (Columns - 1) * GapMm;
        float gridHeight = Rows * CardHeightMm + (Rows - 1) * GapMm;

        container
            .AlignCenter()
            .AlignMiddle()
            .Width(gridWidth, Unit.Millimetre)
            .Height(gridHeight, Unit.Millimetre)
            .Column(page =>
            {
                page.Spacing(GapMm, Unit.Millimetre);

                for (int row = 0; row < Rows; row++)
                {
                    page.Item().Row(r =>
                    {
                        r.Spacing(GapMm, Unit.Millimetre);

                        for (int col = 0; col < Columns; col++)
                        {
                            int index = row * Columns + col;

                            r.ConstantItem(CardWidthMm, Unit.Millimetre)
                             .Height(CardHeightMm, Unit.Millimetre)
                             .Element(slot =>
                             {
                                 if (index < cards.Count)
                                 {
                                     slot.Component(new SpellCardComponent(cards[index]));
                                 }
                                 // else: empty slot, intentionally left blank
                             });
                        }
                    });
                }
            });
    }
}
