using System.Text;
using MorseApp.Native;
using MorseApp.Services;
using Xunit;

namespace MorseCore.Tests;

/// <summary>Collects one row per check so the real results can be written into docs/Chapter3_TestCases.md.</summary>
public sealed class ReportFixture : IDisposable
{
    public readonly List<(string Id, string Routine, string Input, string Expected, string Actual, bool Pass)> Rows = new();
    private readonly object _lock = new();

    public void Record(string id, string routine, string input, string expected, string actual)
    {
        lock (_lock) Rows.Add((id, routine, input, expected, actual, expected == actual));
        Assert.True(expected == actual, $"{id} {routine}({input}): expected {expected}, got {actual}");
    }

    public void Dispose()
    {
        string? path = Environment.GetEnvironmentVariable("MORSE_TEST_REPORT");
        if (string.IsNullOrEmpty(path)) return;
        var sb = new StringBuilder();
        sb.AppendLine("| ID | Routine | Input | Expected | Actual | Result |");
        sb.AppendLine("| :-- | :-- | :-- | :-- | :-- | :-: |");
        foreach (var r in Rows.OrderBy(r => r.Id, StringComparer.Ordinal))
            sb.AppendLine($"| {r.Id} | `{r.Routine}` | {r.Input} | {r.Expected} | {r.Actual} | {(r.Pass ? "PASS" : "FAIL")} |");
        File.WriteAllText(path, sb.ToString());
    }
}

[CollectionDefinition("native")]
public class NativeCollection : ICollectionFixture<ReportFixture> { }

[Collection("native")]
public class MorseCoreTests
{
    private readonly ReportFixture _r;
    public MorseCoreTests(ReportFixture r) => _r = r;

    // Independent oracle: International Morse (ITU-R M.1677-1) written out by hand.
    private static readonly Dictionary<char, string> Oracle = new()
    {
        ['A'] = ".-", ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..", ['E'] = ".", ['F'] = "..-.", ['G'] = "--.", ['H'] = "....",
        ['I'] = "..", ['J'] = ".---", ['K'] = "-.-", ['L'] = ".-..", ['M'] = "--", ['N'] = "-.", ['O'] = "---", ['P'] = ".--.",
        ['Q'] = "--.-", ['R'] = ".-.", ['S'] = "...", ['T'] = "-", ['U'] = "..-", ['V'] = "...-", ['W'] = ".--", ['X'] = "-..-",
        ['Y'] = "-.--", ['Z'] = "--..",
        ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-", ['5'] = ".....",
        ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----.",
        ['.'] = ".-.-.-", [','] = "--..--", ['?'] = "..--.."
    };

    private static string Pattern(ushort packed)
    {
        int count = packed & 0xFF;
        int pattern = (packed >> 8) & 0xFF;
        var sb = new StringBuilder();
        for (int i = count - 1; i >= 0; i--) sb.Append(((pattern >> i) & 1) == 1 ? '-' : '.');
        return sb.ToString();
    }

    public static IEnumerable<object[]> AllChars() => Oracle.Select(kv => new object[] { kv.Key, kv.Value });

    // ------------------------------------------------------------------ encoder
    [Theory, MemberData(nameof(AllChars))]
    public void Encode_matches_international_morse(char c, string expected)
        => _r.Record($"ENC-{c}", "Morse_EncodeChar", $"'{c}'", expected, Pattern(MorseEngineService.EncodePacked(c)));

    [Theory, MemberData(nameof(AllChars))]
    public void Decode_round_trips_every_character(char c, string _)
    {
        var p = MorseEngineService.EncodeChar(c);
        _r.Record($"RT-{c}", "Morse_DecodeChar(Morse_EncodeChar)", $"'{c}'", $"'{c}'", $"'{MorseEngineService.DecodeChar(p.Count, p.Pattern)}'");
    }

    [Fact]
    public void Encode_folds_lowercase()
    {
        for (char c = 'a'; c <= 'z'; c++)
            Assert.Equal(MorseEngineService.EncodePacked(char.ToUpper(c)), MorseEngineService.EncodePacked(c));
        _r.Record("ENC-LOWER", "Morse_EncodeChar", "'a'..'z'", "equals 'A'..'Z'", "equals 'A'..'Z'");
    }

    [Theory]
    [InlineData(' ', "ENC-SPACE")]
    [InlineData('~', "ENC-TILDE")]
    [InlineData('!', "ENC-BANG")]
    [InlineData('\0', "ENC-NUL")]
    public void Encode_unmapped_returns_zero(char c, string id)
        => _r.Record(id, "Morse_EncodeChar", $"0x{(int)c:X2}", "0x0000", $"0x{MorseEngineService.EncodePacked(c):X4}");

    [Fact]
    public void Encode_known_limitation_non_ascii_aliases_onto_ascii()
    {
        // The encoder masks the index with 7Fh, so 0xC1 ('A' + 0x80) maps to 'A'. The application only ever passes
        // ASCII characters; this documents the behaviour rather than endorsing it.
        _r.Record("ENC-ALIAS", "Morse_EncodeChar", "0xC1 (non-ASCII)", "same as 'A' (known limitation)",
            MorseEngineService.EncodePacked((char)0xC1) == MorseEngineService.EncodePacked('A') ? "same as 'A' (known limitation)" : "different");
    }

    [Fact]
    public void EncodeString_sos()
    {
        var w = MorseEngineService.EncodeString("SOS", 8);
        _r.Record("ENCSTR-SOS", "Morse_EncodeString", "\"SOS\", max 8", "... --- ...", string.Join(" ", w.Select(Pattern)));
    }

    [Fact]
    public void EncodeString_stops_at_nul()
        => _r.Record("ENCSTR-NUL", "Morse_EncodeString", "\"AB\\0C\", max 8", "2 words", $"{MorseEngineService.EncodeString("AB\0C", 8).Length} words");

    [Fact]
    public void EncodeString_respects_capacity()
        => _r.Record("ENCSTR-MAX", "Morse_EncodeString", "\"HELLO\", max 3", "3 words", $"{MorseEngineService.EncodeString("HELLO", 3).Length} words");

    // ------------------------------------------------------------------ decoder edge cases
    [Theory]
    [InlineData(0, 0, "DEC-COUNT0")]
    [InlineData(7, 0, "DEC-COUNT7")]
    [InlineData(255, 1, "DEC-COUNT255")]
    [InlineData(4, 3, "DEC-EMPTYSLOT")]   // ..-- is not a letter
    [InlineData(6, 0, "DEC-6ZERO")]        // 6 elements, not one of . , ?
    public void Decode_invalid_returns_zero(byte count, byte pattern, string id)
        => _r.Record(id, "Morse_DecodeChar", $"count={count}, pattern={pattern}", "0", MorseEngineService.DecodeRaw(count, pattern).ToString());

    [Theory]
    [InlineData(1, 0, 'E')]
    [InlineData(1, 1, 'T')]
    [InlineData(2, 1, 'A')]
    [InlineData(2, 2, 'N')]
    [InlineData(6, 0x15, '.')]
    [InlineData(6, 0x33, ',')]
    [InlineData(6, 0x0C, '?')]
    public void Decode_spot_checks(byte count, byte pattern, char expected)
        => _r.Record($"DEC-{count}-{pattern:X2}", "Morse_DecodeChar", $"count={count}, pattern=0x{pattern:X2}", $"'{expected}'", $"'{MorseEngineService.DecodeChar(count, pattern)}'");

    // ------------------------------------------------------------------ quantizer
    [Theory]
    [InlineData(5)] [InlineData(15)] [InlineData(30)]
    public void QuantizePress_thresholds(int wpm)
    {
        ulong t = MorseEngineService.UnitUs(wpm);
        void Case(string id, ulong dur, MorseToken expected, string label) =>
            _r.Record($"QP-{wpm:00}-{id}", "Morse_QuantizePress", $"{wpm} WPM, t = {label} ({dur} us), T = {t} us", expected.ToString(), MorseEngineService.QuantizePress(dur, t).ToString());

        Case("A", 0, MorseToken.None, "0");
        Case("B", t / 4 - 1, MorseToken.None, "T/4 - 1");
        Case("C", t / 4, MorseToken.Dit, "T/4");
        Case("D", t, MorseToken.Dit, "T");
        Case("E", 2 * t - 1, MorseToken.Dit, "2T - 1");
        Case("F", 2 * t, MorseToken.Dah, "2T");
        Case("G", 3 * t, MorseToken.Dah, "3T");
        Case("H", 6 * t, MorseToken.Dah, "6T");
        Case("I", 6 * t + 1, MorseToken.Invalid, "6T + 1");
        Case("J", ulong.MaxValue, MorseToken.Invalid, "max");
    }

    // ------------------------------------------------------------------ gap evaluator
    [Theory]
    [InlineData(5)] [InlineData(15)] [InlineData(30)]
    public void EvaluateGap_thresholds(int wpm)
    {
        ulong t = MorseEngineService.UnitUs(wpm);
        ulong charGap = 5 * t / 2;
        void Case(string id, ulong gap, GapAction expected, string label) =>
            _r.Record($"GAP-{wpm:00}-{id}", "Morse_EvaluateGap", $"{wpm} WPM, gap = {label} ({gap} us), T = {t} us", expected.ToString(), MorseEngineService.EvaluateGap(gap, t).ToString());

        Case("A", 0, GapAction.Continue, "0");
        Case("B", t, GapAction.Continue, "T (intra-character)");
        Case("C", charGap - 1, GapAction.Continue, "2.5T - 1");
        Case("D", charGap, GapAction.CommitCharacter, "2.5T");
        Case("E", 3 * t, GapAction.CommitCharacter, "3T (ideal letter gap)");
        Case("F", 6 * t - 1, GapAction.CommitCharacter, "6T - 1");
        Case("G", 6 * t, GapAction.CommitSpace, "6T");
        Case("H", 7 * t, GapAction.CommitSpace, "7T (ideal word gap)");
    }

    // ------------------------------------------------------------------ unit math & benchmark
    [Theory]
    [InlineData(5, 240000UL)] [InlineData(15, 80000UL)] [InlineData(20, 60000UL)] [InlineData(30, 40000UL)]
    public void UnitUs_matches_itu_formula(int wpm, ulong expected)
        => _r.Record($"UNIT-{wpm:00}", "UnitUs (T = 1,200,000 / WPM)", $"{wpm} WPM", $"{expected} us", $"{MorseEngineService.UnitUs(wpm)} us");

    [Fact]
    public void Benchmark_counter_is_monotonic()
    {
        ulong prev = MorseEngineService.GetCycles();
        bool ok = true;
        for (int i = 0; i < 1000; i++)
        {
            ulong now = MorseEngineService.GetCycles();
            if (now < prev) ok = false;
            prev = now;
        }
        _r.Record("BENCH-MONO", "Morse_GetBenchmarkCycles", "1000 consecutive reads", "non-decreasing", ok ? "non-decreasing" : "decreased");
    }
}
