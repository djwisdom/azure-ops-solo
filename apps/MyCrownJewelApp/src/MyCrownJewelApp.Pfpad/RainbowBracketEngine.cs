using System.Collections.ObjectModel;

namespace MyCrownJewelApp.Pfpad;

public enum BracketPairState
{
    Matched,
    Mismatched,
    Unmatched
}

public sealed record BracketInfo(
    int Position,
    char Char,
    int Depth,
    bool IsOpen,
    int? PairIndex,
    BracketPairState PairState
);

public sealed record RainbowBracketResult(
    int Version,
    IReadOnlyList<BracketInfo> Brackets,
    IReadOnlyList<(int OpenIndex, int CloseIndex)> Pairs
)
{
    public int? FindBraceAt(int position)
    {
        for (int i = 0; i < Brackets.Count; i++)
            if (Brackets[i].Position == position)
                return i;
        return null;
    }

    public BracketInfo? GetPairFor(int index)
    {
        if (index < 0 || index >= Brackets.Count) return null;
        var b = Brackets[index];
        if (b.PairIndex == null) return null;
        return Brackets[b.PairIndex.Value];
    }
}

internal enum ParseState
{
    Normal,
    LineComment,
    BlockComment,
    InString,
    InChar
}

public sealed class RainbowBracketEngine
{
    private static readonly HashSet<char> _openBraces = ['{', '[', '('];
    private static readonly Dictionary<char, char> _closeToOpen = new()
    {
        ['}'] = '{',
        [']'] = '[',
        [')'] = '('
    };

    public static RainbowBracketResult Parse(string text, int version)
    {
        var brackets = new List<BracketInfo>();
        var pairs = new List<(int OpenIndex, int CloseIndex)>();
        var stack = new Stack<(char Bracket, int Position, int Index)>();
        int unclosedCount = 0;
        ParseState state = ParseState.Normal;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            switch (state)
            {
                case ParseState.LineComment:
                    if (c == '\n') state = ParseState.Normal;
                    continue;
                case ParseState.BlockComment:
                    if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        i++;
                        state = ParseState.Normal;
                    }
                    continue;
                case ParseState.InString:
                    if (c == '\\') { i++; continue; }
                    if (c == '"') state = ParseState.Normal;
                    continue;
                case ParseState.InChar:
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') state = ParseState.Normal;
                    continue;
            }

            if (c == '/' && i + 1 < text.Length)
            {
                if (text[i + 1] == '/')
                {
                    state = ParseState.LineComment;
                    i++;
                    continue;
                }
                if (text[i + 1] == '*')
                {
                    state = ParseState.BlockComment;
                    i++;
                    continue;
                }
            }

            if (c == '"')
            {
                state = ParseState.InString;
                continue;
            }

            if (c == '\'')
            {
                state = ParseState.InChar;
                continue;
            }

            if (_openBraces.Contains(c))
            {
                unclosedCount++;
                int depth = unclosedCount;
                int bracketIndex = brackets.Count;
                stack.Push((c, i, bracketIndex));
                brackets.Add(new BracketInfo(i, c, depth, true, null, BracketPairState.Unmatched));
            }
            else if (_closeToOpen.TryGetValue(c, out var open))
            {
                if (stack.Count > 0 && stack.Peek().Bracket == open)
                {
                    var (_, openPos, openIndex) = stack.Pop();
                    int depth = unclosedCount;
                    unclosedCount--;
                    brackets[openIndex] = brackets[openIndex] with
                    {
                        Depth = depth,
                        PairIndex = brackets.Count,
                        PairState = BracketPairState.Matched
                    };
                    brackets.Add(new BracketInfo(i, c, depth, false, openIndex, BracketPairState.Matched));
                    pairs.Add((openIndex, brackets.Count - 1));
                }
                else
                {
                    brackets.Add(new BracketInfo(i, c, 0, false, null, BracketPairState.Mismatched));
                }
            }
        }

        return new RainbowBracketResult(version, brackets.AsReadOnly(), pairs.AsReadOnly());
    }

    public static readonly Color[] DefaultPalette =
    [
        Color.FromArgb(204, 85, 85),
        Color.FromArgb(204, 170, 85),
        Color.FromArgb(170, 204, 85),
        Color.FromArgb(85, 204, 85),
        Color.FromArgb(85, 170, 204),
        Color.FromArgb(85, 85, 204),
        Color.FromArgb(170, 85, 204),
        Color.FromArgb(204, 85, 170),
    ];

    public static readonly Color[] HighContrastPalette =
    [
        Color.FromArgb(255, 80, 80),
        Color.FromArgb(255, 200, 80),
        Color.FromArgb(180, 255, 80),
        Color.FromArgb(80, 255, 80),
        Color.FromArgb(80, 200, 255),
        Color.FromArgb(80, 80, 255),
        Color.FromArgb(200, 80, 255),
        Color.FromArgb(255, 80, 200),
    ];
}
