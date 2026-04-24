using Calamari.Common.Plumbing.Extensions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util;

// Ported from lodash 4.17.21 tests
// https://github.com/lodash/lodash/blob/4.17.21/test/test.js
[TestFixture]
public class LodashStringExtensionsFixture
{
    [TestCase("12 feet",          "12Feet")]
    [TestCase("enable 6h format", "enable6HFormat")]
    [TestCase("enable 24H format", "enable24HFormat")]
    [TestCase("too legit 2 quit", "tooLegit2Quit")]
    [TestCase("walk 500 miles",   "walk500Miles")]
    [TestCase("xhr2 request",     "xhr2Request")]
    public void CamelCase_WorksWithNumbers(string input, string expected)
    {
        input.CamelCase().Should().Be(expected);
    }

    [TestCase("safe HTML",                        "safeHtml")]
    [TestCase("safeHTML",                         "safeHtml")]
    [TestCase("escape HTML entities",             "escapeHtmlEntities")]
    [TestCase("escapeHTMLEntities",               "escapeHtmlEntities")]
    [TestCase("XMLHttpRequest",                   "xmlHttpRequest")]
    [TestCase("XmlHTTPRequest",                   "xmlHttpRequest")]
    [TestCase("-only-$AlphaNUMERIC-characters%^", "onlyAlphaNumericCharacters")]
    public void CamelCase_HandlesAcronyms(string input, string expected)
    {
        input.CamelCase().Should().Be(expected);
    }

    [TestCase("to_camel_case_function", "toCamelCaseFunction")]
    public void CamelCase_SplitsOnUnderscores(string input, string expected)
    {
        input.CamelCase().Should().Be(expected);
    }

    static readonly string[] BurredLetters =
    [
        // Latin-1 Supplement letters.
        "À", "Á", "Â", "Ã", "Ä", "Å", "Æ", "Ç", "È", "É", "Ê", "Ë", "Ì", "Í", "Î", "Ï",
        "Ð", "Ñ", "Ò", "Ó", "Ô", "Õ", "Ö",           "Ø", "Ù", "Ú", "Û", "Ü", "Ý", "Þ", "ß",
        "à", "á", "â", "ã", "ä", "å", "æ", "ç", "è", "é", "ê", "ë", "ì", "í", "î", "ï",
        "ð", "ñ", "ò", "ó", "ô", "õ", "ö",           "ø", "ù", "ú", "û", "ü", "ý", "þ", "ÿ",
        // Latin Extended-A letters.
        "Ā", "ā", "Ă", "ă", "Ą", "ą", "Ć", "ć", "Ĉ", "ĉ", "Ċ", "ċ", "Č", "č", "Ď", "ď",
        "Đ", "đ", "Ē", "ē", "Ĕ", "ĕ", "Ė", "ė", "Ę", "ę", "Ě", "ě", "Ĝ", "ĝ", "Ğ", "ğ",
        "Ġ", "ġ", "Ģ", "ģ", "Ĥ", "ĥ", "Ħ", "ħ", "Ĩ", "ĩ", "Ī", "ī", "Ĭ", "ĭ", "Į", "į",
        "İ", "ı", "Ĳ", "ĳ", "Ĵ", "ĵ", "Ķ", "ķ", "ĸ", "Ĺ", "ĺ", "Ļ", "ļ", "Ľ", "ľ", "Ŀ",
        "ŀ", "Ł", "ł", "Ń", "ń", "Ņ", "ņ", "Ň", "ň", "ŉ", "Ŋ", "ŋ", "Ō", "ō", "Ŏ", "ŏ",
        "Ő", "ő", "Œ", "œ", "Ŕ", "ŕ", "Ŗ", "ŗ", "Ř", "ř", "Ś", "ś", "Ŝ", "ŝ", "Ş", "ş",
        "Š", "š", "Ţ", "ţ", "Ť", "ť", "Ŧ", "ŧ", "Ũ", "ũ", "Ū", "ū", "Ŭ", "ŭ", "Ů", "ů",
        "Ű", "ű", "Ų", "ų", "Ŵ", "ŵ", "Ŷ", "ŷ", "Ÿ", "Ź", "ź", "Ż", "ż", "Ž", "ž", "ſ"
    ];

    static readonly string[] DeburredLetters =
    [
        // Converted Latin-1 Supplement letters.
        "A",  "A", "A", "A", "A", "A", "Ae", "C",  "E", "E", "E", "E", "I", "I", "I",
        "I",  "D", "N", "O", "O", "O", "O",  "O",  "O", "U", "U", "U", "U", "Y", "Th",
        "ss", "a", "a", "a", "a", "a", "a",  "ae", "c", "e", "e", "e", "e", "i", "i", "i",
        "i",  "d", "n", "o", "o", "o", "o",  "o",  "o", "u", "u", "u", "u", "y", "th", "y",
        // Converted Latin Extended-A letters.
        "A", "a", "A", "a", "A", "a", "C", "c", "C", "c", "C", "c", "C", "c",
        "D", "d", "D", "d", "E", "e", "E", "e", "E", "e", "E", "e", "E", "e",
        "G", "g", "G", "g", "G", "g", "G", "g", "H", "h", "H", "h",
        "I", "i", "I", "i", "I", "i", "I", "i", "I", "i", "IJ", "ij", "J", "j",
        "K", "k", "k", "L", "l", "L", "l", "L", "l", "L", "l", "L", "l",
        "N", "n", "N", "n", "N", "n", "'n", "N", "n",
        "O", "o", "O", "o", "O", "o", "Oe", "oe",
        "R", "r", "R", "r", "R", "r", "S", "s", "S", "s", "S", "s", "S", "s",
        "T", "t", "T", "t", "T", "t",
        "U", "u", "U", "u", "U", "u", "U", "u", "U", "u", "U", "u",
        "W", "w", "Y", "y", "Y", "Z", "z", "Z", "z", "Z", "z", "s"
    ];

    [Test]
    public void CamelCase_DeburrsLatinLetters()
    {
        BurredLetters.Length.Should().Be(DeburredLetters.Length, "burred/deburred fixtures must be aligned");

        for (var i = 0; i < BurredLetters.Length; i++)
        {
            // caseMethods test: the camelCase branch lowercases the deburred letter
            // after stripping apostrophes (e.g. 'n → n).
            var expected = DeburredLetters[i].Replace("'", string.Empty).ToLowerInvariant();
            BurredLetters[i].CamelCase().Should().Be(expected, $"index {i}: burred='{BurredLetters[i]}' deburred='{DeburredLetters[i]}'");
        }
    }

    [TestCase("'")]
    [TestCase("’")]
    public void CamelCase_RemovesContractionApostrophes(string apostrophe)
    {
        foreach (var postfix in new[] { "d", "ll", "m", "re", "s", "t", "ve" })
        {
            var input = "a b" + apostrophe + postfix + " c";
            var expected = "aB" + postfix + "C";
            input.CamelCase().Should().Be(expected, $"postfix='{postfix}' apostrophe=U+{(int)apostrophe[0]:X4}");
        }
    }

    [TestCase("×")] // ×
    [TestCase("÷")] // ÷
    public void CamelCase_RemovesLatinMathOperators(string input)
    {
        input.CamelCase().Should().Be(string.Empty);
    }

    [Test]
    public void CamelCase_OnNullReturnsEmpty()
    {
        ((string)null).CamelCase().Should().Be(string.Empty);
    }

    [Test]
    public void CamelCase_OnEmptyReturnsEmpty()
    {
        string.Empty.CamelCase().Should().Be(string.Empty);
    }

    [Test]
    public void CamelCase_PreservesAstralSymbols()
    {
        const string hearts     = "💕";
        const string leafs      = "🍂";
        const string rocket     = "🚀";
        // 👨‍❤️‍💋‍👨 (kiss: man, man) — man + ZWJ + heart + emoji-var + ZWJ + kiss mark + ZWJ + man
        const string comboGlyph = "👨‍❤️‍💋‍👨";

        (hearts + " the " + leafs).CamelCase().Should().Be(hearts + "The" + leafs);

        const string input = "A " + leafs + ", " + comboGlyph + ", and " + rocket;
        input.CamelCase().Should().Be("a" + leafs + comboGlyph + "And" + rocket);
    }
}
