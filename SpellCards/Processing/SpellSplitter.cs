using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SpellCards.Models;
using SkiaSharp;

public static class SpellSplitter
{
    private const float CardWidthMillimetres = 63f;
    private const float CardHeightMillimetres = 88f;
    private const float PointsPerMillimetre = 72f / 25.4f;

    private const float OuterPadding = 6f;           // applied on every side
    private const float StripeWidth = 3f;            // school stripe on the left
    private const float ContentPaddingLeft = 6f;     // padding between stripe and text column
    private const float SectionSpacing = 1.5f;         // spacing between major blocks
    private const float MetadataLineSpacing = 1.5f;  // spacing within metadata column
    private const float MetadataInnerSpacing = 1f;   // spacing inside metadata columns
    private const float MetadataColumnGapPoints = 3f;
    private const float IconSize = 14f;              // icon square in the header
    private const float TagPadding = 4f;             // PaddingVertical(2)
    private const float NotesPaddingTop = 2f;        // PaddingTop(2)
    private const float DescriptionSeparatorThickness = 0.6f;
    private const float DescriptionSeparatorPadding = 2f;
    private const string PlaceholderPartLabel = "(99/99)";
    private const float MeasurementTolerance = 1.5f;

    private static readonly float CardHeightPoints = CardHeightMillimetres * PointsPerMillimetre;
    private static readonly float CardWidthPoints = CardWidthMillimetres * PointsPerMillimetre;
    private static readonly float ContentWidth = CardWidthPoints - (OuterPadding * 2) - StripeWidth - ContentPaddingLeft;
    private static readonly float HeaderTextWidth = ContentWidth - IconSize;
    private static readonly float VerticalPadding = OuterPadding * 2; // top + bottom from .Padding(6)

    private static readonly SKTypeface CinzelTypeface = LoadTypeface("Cinzel-SemiBold.ttf");
    private static readonly SKTypeface SerifRegularTypeface = LoadTypeface("SourceSerif4-Regular.ttf");
    private static readonly SKTypeface SerifSemiboldTypeface = LoadTypeface("SourceSerif4-Semibold.ttf");

    private static readonly TextMetric HeaderNameMetric = new(CinzelTypeface, 11f, 1.15f);
    private static readonly TextMetric HeaderPartMetric = new(SerifRegularTypeface, 7f, 1.1f);
    private static readonly TextMetric MetadataLargeMetric = new(SerifRegularTypeface, 7f, 1.15f);
    private static readonly TextMetric MetadataMetric = new(SerifRegularTypeface, 6.5f, 1.15f);
    private static readonly TextMetric TagMetric = new(SerifSemiboldTypeface, 6f, 1.1f);
    private static readonly TextMetric DescriptionMetric = new(SerifRegularTypeface, 7f, 1.05f);
    private static readonly TextMetric NotesMetric = new(SerifRegularTypeface, 6f, 1.1f);

    public static IReadOnlyList<Spell> SplitIfNeeded(Spell spell)
    {
        var cleanDescription = Normalize(spell.Description);
        var hasOriginalPart = !string.IsNullOrWhiteSpace(spell.Part);

        if (Fits(spell, cleanDescription, hasOriginalPart))
            return new[] { spell with { Description = cleanDescription } };

        var parts = SplitByHeight(spell, cleanDescription);

        return parts
            .Select((text, index) => spell with
            {
                Part = $"{index + 1}/{parts.Count}",
                Description = text
            })
            .ToList();
    }

    private static IReadOnlyList<string> SplitByHeight(Spell spell, string description)
    {
        var sentences = TokenizeSentences(description);
        var result = new List<string>();
        var buffer = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (TryAppend(buffer, sentence, spell, forcePartLine: true))
                continue;

            if (buffer.Length > 0)
            {
                result.Add(buffer.ToString().Trim());
                buffer.Clear();

                if (TryAppend(buffer, sentence, spell, forcePartLine: true))
                    continue;
            }

            foreach (var chunk in SplitByWords(sentence, spell))
                result.Add(chunk);
        }

        if (buffer.Length > 0)
            result.Add(buffer.ToString().Trim());

        return result;
    }

    private static IEnumerable<string> SplitByWords(string sentence, Spell spell)
    {
        var words = sentence
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            yield break;

        var buffer = new StringBuilder();

        foreach (var word in words)
        {
            if (TryAppend(buffer, word, spell, forcePartLine: true))
                continue;

            if (buffer.Length > 0)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();

                if (TryAppend(buffer, word, spell, forcePartLine: true))
                    continue;
            }

            foreach (var chunk in SplitByCharacters(word, spell))
            {
                if (TryAppend(buffer, chunk, spell, forcePartLine: true))
                    continue;

                if (buffer.Length > 0)
                {
                    yield return buffer.ToString().Trim();
                    buffer.Clear();
                }

                if (!TryAppend(buffer, chunk, spell, forcePartLine: true))
                    yield return chunk.Trim();
            }
        }

        if (buffer.Length > 0)
            yield return buffer.ToString().Trim();
    }

    private static IEnumerable<string> SplitByCharacters(string fragment, Spell spell)
    {
        var buffer = new StringBuilder();

        foreach (var ch in fragment)
        {
            buffer.Append(ch);

            if (Fits(spell, buffer.ToString(), forcePartLine: true))
                continue;

            if (buffer.Length <= 1)
                throw new InvalidOperationException("Spell card layout leaves no space for description text. Increase card size or adjust fonts.");

            var overflowChar = buffer[^1];
            buffer.Length -= 1;
            yield return buffer.ToString().Trim();
            buffer.Clear();
            buffer.Append(overflowChar);
        }

        if (buffer.Length > 0)
            yield return buffer.ToString().Trim();
    }

    private static bool TryAppend(StringBuilder buffer, string fragment, Spell spell, bool forcePartLine)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return true;

        var candidate = buffer.Length == 0
            ? fragment.Trim()
            : $"{buffer} {fragment.Trim()}";

        if (!Fits(spell, candidate, forcePartLine))
            return false;

        buffer.Clear();
        buffer.Append(candidate);
        return true;
    }

    private static bool Fits(Spell spell, string description, bool forcePartLine)
    {
        var totalHeight = MeasureCardHeight(spell, description, forcePartLine);
        return totalHeight <= (CardHeightPoints + MeasurementTolerance);
    }

    private static float MeasureCardHeight(Spell spell, string description, bool forcePartLine)
    {
        var blocks = new List<float>
        {
            MeasureHeader(spell.Name, spell.Part, forcePartLine)
        };

        var metadataHeight = MeasureMetadataBlock(spell);
        if (metadataHeight > 0)
            blocks.Add(metadataHeight);

        var tagsHeight = MeasureTags(spell.Tags);
        if (tagsHeight > 0)
            blocks.Add(tagsHeight);

        var descriptionHeight = MeasureDescription(description);
        if (descriptionHeight > 0)
        {
            var separatorHeight = DescriptionSeparatorThickness + DescriptionSeparatorPadding;
            if (metadataHeight > 0 || tagsHeight > 0)
                blocks.Add(separatorHeight);

            blocks.Add(descriptionHeight);
        }

        var notesHeight = MeasureNotes(spell.Notes);
        if (notesHeight > 0)
            blocks.Add(notesHeight);

        var contentHeight = SumWithSpacing(blocks, SectionSpacing);
        return VerticalPadding + contentHeight;
    }

    private static float MeasureHeader(string name, string? part, bool forcePartLine)
    {
        var lines = new List<float>
        {
            MeasureText(name, HeaderNameMetric, HeaderTextWidth)
        };

        if (forcePartLine || !string.IsNullOrWhiteSpace(part))
        {
            var text = string.IsNullOrWhiteSpace(part) ? PlaceholderPartLabel : $"({part})";
            lines.Add(MeasureText(text, HeaderPartMetric, HeaderTextWidth));
        }

        var textHeight = lines.Sum();
        return Math.Max(IconSize, textHeight);
    }

    private static float MeasureMetadataBlock(Spell spell)
    {
        var topLines = new List<float>();

        var classHeight = MeasureText(spell.ClassLevel, MetadataLargeMetric, ContentWidth);
        if (classHeight > 0)
            topLines.Add(classHeight);

        var schoolHeight = MeasureText(spell.SchoolText, MetadataLargeMetric, ContentWidth);
        if (schoolHeight > 0)
            topLines.Add(schoolHeight);

        var topHeight = SumWithSpacing(topLines, MetadataLineSpacing);

        var detailLines = BuildMetadataDetails(spell);
        var columnsHeight = MeasureMetadataColumns(detailLines);

        if (columnsHeight > 0 && topHeight > 0)
            return topHeight + MetadataLineSpacing + columnsHeight;

        return topHeight > 0 ? topHeight : columnsHeight;
    }

    private static float MeasureTags(string tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return 0f;

        var textHeight = MeasureText(tags, TagMetric, ContentWidth);
        return textHeight <= 0 ? 0 : textHeight + TagPadding;
    }

    private static float MeasureNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return 0f;

        var textHeight = MeasureText(notes, NotesMetric, ContentWidth);
        return textHeight <= 0 ? 0 : NotesPaddingTop + textHeight;
    }

    private static float MeasureDescription(string text)
    {
        return MeasureText(text, DescriptionMetric, ContentWidth);
    }

    private static float MeasureText(string text, TextMetric metric, float width)
    {
        if (string.IsNullOrWhiteSpace(text) || width <= 0)
            return 0f;

        using var paint = new SKPaint
        {
            Typeface = metric.Typeface,
            TextSize = metric.FontSize,
            IsAntialias = true,
            SubpixelText = true,
            TextAlign = SKTextAlign.Left
        };

        var spaceWidth = paint.MeasureText(" ");
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return 0f;

        float currentWidth = 0f;
        int lineCount = 1;

        foreach (var word in words)
        {
            var segments = MeasureWordSegments(word, width, paint);

            for (int i = 0; i < segments.Count; i++)
            {
                var segmentWidth = segments[i];
                var spacer = (i == 0 && currentWidth > 0) ? spaceWidth : 0f;

                if ((currentWidth + spacer + segmentWidth) <= width)
                {
                    currentWidth += spacer + segmentWidth;
                }
                else
                {
                    lineCount++;
                    currentWidth = segmentWidth;
                }
            }
        }

        return lineCount * metric.FontSize * metric.LineHeight;
    }

    private static List<float> MeasureWordSegments(string word, float maxWidth, SKPaint paint)
    {
        var segments = new List<float>();

        if (maxWidth <= 0)
        {
            segments.Add(paint.MeasureText(word));
            return segments;
        }

        var wordWidth = paint.MeasureText(word);
        if (wordWidth <= maxWidth)
        {
            segments.Add(wordWidth);
            return segments;
        }

        var builder = new StringBuilder();

        foreach (var ch in word)
        {
            builder.Append(ch);
            var currentWidth = paint.MeasureText(builder.ToString());

            if (currentWidth > maxWidth && builder.Length > 1)
            {
                builder.Length -= 1;
                var chunk = builder.ToString();
                segments.Add(paint.MeasureText(chunk));
                builder.Clear();
                builder.Append(ch);
            }
            else if (currentWidth > maxWidth)
            {
                // single character wider than max width, keep as-is
                segments.Add(currentWidth);
                builder.Clear();
            }
        }

        if (builder.Length > 0)
            segments.Add(paint.MeasureText(builder.ToString()));

        return segments;
    }

    private static float SumWithSpacing(IReadOnlyList<float> blocks, float spacing)
    {
        var nonZeroBlocks = blocks.Where(b => b > 0).ToList();
        if (nonZeroBlocks.Count == 0)
            return 0f;

        var total = nonZeroBlocks.Sum();
        var gaps = Math.Max(0, nonZeroBlocks.Count - 1);
        return total + gaps * spacing;
    }

    private static string Normalize(string text)
    {
        var tokens = text
            .Replace("\r", "")
            .Replace("\n", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ",
            tokens.Where(w => w.Length > 1 || char.IsLetterOrDigit(w[0]))).Trim();
    }

    private static IReadOnlyList<string> TokenizeSentences(string text)
    {
        var tokens = Regex
            .Split(text, @"(?<=[.!?])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        return tokens.Count > 0 ? tokens : new List<string> { text };
    }

    private static SKTypeface LoadTypeface(string fileName)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, "assets", "fonts", fileName);
            if (File.Exists(path))
                return SKTypeface.FromFile(path);
        }
        catch
        {
            // fallback to default font below
        }

        return SKTypeface.Default;
    }

    private static List<string> BuildMetadataDetails(Spell spell)
    {
        var details = new List<string>
        {
            $"Cast: {spell.Cast}",
            $"Range: {spell.Range}"
        };

        if (!string.IsNullOrWhiteSpace(spell.TargetOrArea))
            details.Add(spell.TargetOrArea);

        if (!string.IsNullOrWhiteSpace(spell.Duration))
            details.Add($"Duration: {spell.Duration}");

        if (ShouldRenderValue(spell.Save))
            details.Add($"Save: {spell.Save}");

        if (ShouldRenderValue(spell.Sr))
            details.Add($"SR: {spell.Sr}");

        if (!string.IsNullOrWhiteSpace(spell.Components))
            details.Add($"Components: {spell.Components}");

        return details;
    }

    private static bool ShouldRenderValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        return v is not "014" and not "-";
    }

    private static float MeasureMetadataColumns(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return 0f;

        if (lines.Count == 1)
            return MeasureColumnHeight(lines, ContentWidth);

        var splitIndex = (lines.Count + 1) / 2;
        var left = lines.Take(splitIndex).ToList();
        var right = lines.Skip(splitIndex).ToList();

        var columnWidth = (ContentWidth - MetadataColumnGapPoints) / 2f;
        var leftHeight = MeasureColumnHeight(left, columnWidth);
        var rightHeight = MeasureColumnHeight(right, columnWidth);

        return Math.Max(leftHeight, rightHeight);
    }

    private static float MeasureColumnHeight(IReadOnlyList<string> lines, float width)
    {
        if (lines.Count == 0)
            return 0f;

        var heights = lines
            .Select(line => MeasureText(line, MetadataMetric, width))
            .Where(h => h > 0)
            .ToList();

        return SumWithSpacing(heights, MetadataInnerSpacing);
    }

    private sealed record TextMetric(SKTypeface Typeface, float FontSize, float LineHeight);
}
