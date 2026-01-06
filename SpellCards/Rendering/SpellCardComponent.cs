using System.Collections.Generic;
using System.Linq;
using SpellCards.Infrastructure;
using SpellCards.Models;
using SpellCards.Processing;
using SpellCards.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public sealed class SpellCardComponent : IComponent
{
    private readonly Spell _spell;

    public SpellCardComponent(Spell spell)
    {
        _spell = spell;
    }

    public void Compose(IContainer container)
    {
        var schoolColor = SchoolTheme.Color(_spell.SchoolKey);

        container
            .Border(1)
            .BorderColor(Colors.Black)
            .Background(Colors.White)
            .Padding(6)
            .Row(row =>
            {
                // School stripe
                row.ConstantItem(3)
                   .Background(schoolColor);

                row.RelativeItem()
                   .PaddingLeft(6)
                   .Column(col =>
                   {
                       col.Spacing(1.5f);

                       ComposeHeader(col, schoolColor);
                       ComposeMetadata(col);
                       ComposeTags(col, schoolColor);

                       var description = SpellDescriptionFormatter.FormatForCard(_spell.Description);

                       if (!string.IsNullOrWhiteSpace(description))
                       {
                           col.Item()
                              .PaddingVertical(1)
                              .LineHorizontal(0.6f)
                              .LineColor(Colors.Grey.Lighten2);

                           col.Item()
                              .Text(description)
                              .FontSize(7f)
                              .LineHeight(1.05f);
                       }

                       ComposeNotes(col);
                   });
            });
    }

    private void ComposeHeader(ColumnDescriptor col, string schoolColor)
    {
        col.Item().Row(row =>
        {
            var iconPath = SchoolTheme.IconPath(_spell.SchoolKey);

            if (File.Exists(iconPath))
            {
                var svg = SvgTint.TintSvg(
                    File.ReadAllText(iconPath),
                    schoolColor
                );

                row.ConstantItem(14).Height(14).Svg(svg);
            }
            else
            {
                row.ConstantItem(14).Height(14);
            }

            row.RelativeItem().Column(title =>
            {
                title.Item()
                     .Text(_spell.Name)
                     .FontFamily("Cinzel")
                     .FontSize(11)
                     .SemiBold();

                if (!string.IsNullOrWhiteSpace(_spell.Part))
                {
                    title.Item()
                         .Text($"({_spell.Part})")
                         .FontSize(7)
                         .Italic();
                }
            });
        });
    }

    private void ComposeMetadata(ColumnDescriptor col)
    {
        col.Item().Column(meta =>
        {
            meta.Spacing(1.5f);

            meta.Item().Text(_spell.ClassLevel).FontSize(7);
            meta.Item().Text(_spell.SchoolText).FontSize(7);

            var details = BuildDetailMetadata();
            if (details.Count == 0)
                return;

            meta.Item().Row(row =>
            {
                row.Spacing(3);

                var splitIndex = (details.Count + 1) / 2;
                var left = details.Take(splitIndex).ToList();
                var right = details.Skip(splitIndex).ToList();

                row.RelativeItem().Column(leftCol =>
                {
                    leftCol.Spacing(1);
                    foreach (var line in left)
                        leftCol.Item().Text(line).FontSize(6.3f);
                });

                if (right.Count > 0)
                {
                    row.RelativeItem().Column(rightCol =>
                    {
                        rightCol.Spacing(1);
                        foreach (var line in right)
                            rightCol.Item().Text(line).FontSize(6.3f);
                    });
                }
            });
        });
    }

    private List<string> BuildDetailMetadata()
    {
        var details = new List<string>
        {
            $"Cast: {_spell.Cast}",
            $"Range: {_spell.Range}"
        };

        if (!string.IsNullOrWhiteSpace(_spell.TargetOrArea))
            details.Add(_spell.TargetOrArea);

        if (!string.IsNullOrWhiteSpace(_spell.Duration))
            details.Add($"Duration: {_spell.Duration}");

        if (ShouldRenderValue(_spell.Save))
            details.Add($"Save: {_spell.Save}");

        if (ShouldRenderValue(_spell.Sr))
            details.Add($"SR: {_spell.Sr}");

        if (!string.IsNullOrWhiteSpace(_spell.Components))
            details.Add($"Components: {_spell.Components}");

        return details;
    }

    private static bool ShouldRenderValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        return v is not "—" and not "-";
    }

    private void ComposeTags(ColumnDescriptor col, string schoolColor)
    {
        if (string.IsNullOrWhiteSpace(_spell.Tags))
            return;

        col.Item()
           .PaddingVertical(2)
           .Text(_spell.Tags)
           .FontSize(6)
           .FontColor(schoolColor);
    }

    private void ComposeNotes(ColumnDescriptor col)
    {
        if (string.IsNullOrWhiteSpace(_spell.Notes))
            return;

        col.Item()
           .PaddingTop(2)
           .Text(_spell.Notes)
           .FontSize(6)
           .Italic();
    }
}
