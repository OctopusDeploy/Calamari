using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Calamari.Common.Plumbing.Extensions;

// Port of lodash 4.17.21's camelCase
// https://github.com/lodash/lodash/blob/4.17.21/lodash.js
// This was used in SPF steps to create AWS CloudFormation stack identifiers
// and was brought across to generate the same camel cased strings
public static class LodashStringExtensions
{
    // Character-class ranges.
    const string RsAstralRange         = @"\ud800-\udfff";
    const string RsComboMarksRange     = @"̀-ͯ";
    const string ReComboHalfMarksRange = @"︠-︯";
    const string RsComboSymbolsRange   = @"⃐-⃿";
    const string RsComboRange          = RsComboMarksRange + ReComboHalfMarksRange + RsComboSymbolsRange;
    const string RsDingbatRange        = @"✀-➿";
    const string RsLowerRange          = @"a-z\xdf-\xf6\xf8-\xff";
    const string RsMathOpRange         = @"\xac\xb1\xd7\xf7";
    const string RsNonCharRange        = @"\x00-\x2f\x3a-\x40\x5b-\x60\x7b-\xbf";
    const string RsPunctuationRange    = @" -⁯";
    const string RsSpaceRange          = @" \t\x0b\f\xa0﻿\n\r   ᠎             　";
    const string RsUpperRange          = @"A-Z\xc0-\xd6\xd8-\xde";
    const string RsVarRange            = @"︎️";
    const string RsBreakRange          = RsMathOpRange + RsNonCharRange + RsPunctuationRange + RsSpaceRange;

    // Capture-group building blocks.
    const string RsApos      = "['’]";
    const string RsBreak     = "[" + RsBreakRange + "]";
    const string RsCombo     = "[" + RsComboRange + "]";
    const string RsDigits    = @"\d+";
    const string RsDingbat   = "[" + RsDingbatRange + "]";
    const string RsLower     = "[" + RsLowerRange + "]";
    const string RsMisc      = "[^" + RsAstralRange + RsBreakRange + RsDigits + RsDingbatRange + RsLowerRange + RsUpperRange + "]";
    const string RsFitz      = @"\ud83c[\udffb-\udfff]";
    const string RsModifier  = "(?:" + RsCombo + "|" + RsFitz + ")";
    const string RsNonAstral = "[^" + RsAstralRange + "]";
    const string RsRegional  = @"(?:\ud83c[\udde6-\uddff]){2}";
    const string RsSurrPair  = @"[\ud800-\udbff][\udc00-\udfff]";
    const string RsUpper     = "[" + RsUpperRange + "]";
    const string RsZWJ       = @"‍";

    // Composed building blocks.
    const string RsMiscLower     = "(?:" + RsLower + "|" + RsMisc + ")";
    const string RsMiscUpper     = "(?:" + RsUpper + "|" + RsMisc + ")";
    const string RsOptContrLower = "(?:" + RsApos + "(?:d|ll|m|re|s|t|ve))?";
    const string RsOptContrUpper = "(?:" + RsApos + "(?:D|LL|M|RE|S|T|VE))?";
    const string ReOptMod        = RsModifier + "?";
    const string RsOptVar        = "[" + RsVarRange + "]?";
    const string RsOptJoin       = "(?:" + RsZWJ + "(?:" + RsNonAstral + "|" + RsRegional + "|" + RsSurrPair + ")" + RsOptVar + ReOptMod + ")*";
    const string RsOrdLower      = @"\d*(?:1st|2nd|3rd|(?![123])\dth)(?=\b|[A-Z_])";
    const string RsOrdUpper      = @"\d*(?:1ST|2ND|3RD|(?![123])\dTH)(?=\b|[a-z_])";
    const string RsSeq           = RsOptVar + ReOptMod + RsOptJoin;
    const string RsEmoji         = "(?:" + RsDingbat + "|" + RsRegional + "|" + RsSurrPair + ")" + RsSeq;

    // Final word pattern. Eight alternates joined with | — side-by-side with lodash's
    // RegExp([...].join('|'), 'g') composition so the two can be diffed.
    const string UnicodeWordPattern =
        RsUpper + "?" + RsLower + "+" + RsOptContrLower + "(?=" + RsBreak + "|" + RsUpper + "|$)" +
        "|" + RsMiscUpper + "+" + RsOptContrUpper + "(?=" + RsBreak + "|" + RsUpper + RsMiscLower + "|$)" +
        "|" + RsUpper + "?" + RsMiscLower + "+" + RsOptContrLower +
        "|" + RsUpper + "+" + RsOptContrUpper +
        "|" + RsOrdUpper +
        "|" + RsOrdLower +
        "|" + RsDigits +
        "|" + RsEmoji;

    // Gate regex that decides between the Unicode and ASCII tokenizers.
    const string HasUnicodeWordPattern = @"[a-z][A-Z]|[A-Z]{2}[a-z]|[0-9][a-zA-Z]|[a-zA-Z][0-9]|[^a-zA-Z0-9 ]";

    // ASCII fast path: any run of chars outside the control/punctuation blocks.
    const string AsciiWordPattern = @"[^\x00-\x2f\x3a-\x40\x5b-\x60\x7b-\x7f]+";

    // Latin-1 + Extended-A letters subject to deburring.
    const string LatinPattern = @"[\xc0-\xd6\xd8-\xf6\xf8-\xffĀ-ſ]";

    // ECMAScript mode for JS parity: lodash's regexes don't use the `u` flag, so \d means [0-9]
    // and \b is an ASCII word boundary. .NET's default mode applies Unicode semantics to both.
    const RegexOptions EcmaScript = RegexOptions.ECMAScript;

    static readonly Regex ReAsciiWord      = new(AsciiWordPattern,      EcmaScript);
    static readonly Regex ReLatin          = new(LatinPattern,          EcmaScript);
    static readonly Regex ReApos           = new(RsApos,                EcmaScript);
    static readonly Regex ReComboMark      = new(RsCombo,               EcmaScript);
    static readonly Regex ReUnicodeWord    = new(UnicodeWordPattern,    EcmaScript);
    static readonly Regex ReHasUnicodeWord = new(HasUnicodeWordPattern, EcmaScript);

    // Latin-1 Supplement + Latin Extended-A → basic Latin.
    static readonly Dictionary<char, string> DeburredLetters = new()
    {
        // Latin-1 Supplement block.
        ['À'] = "A",  ['Á'] = "A",  ['Â'] = "A",  ['Ã'] = "A",  ['Ä'] = "A",  ['Å'] = "A",
        ['à'] = "a",  ['á'] = "a",  ['â'] = "a",  ['ã'] = "a",  ['ä'] = "a",  ['å'] = "a",
        ['Ç'] = "C",  ['ç'] = "c",
        ['Ð'] = "D",  ['ð'] = "d",
        ['È'] = "E",  ['É'] = "E",  ['Ê'] = "E",  ['Ë'] = "E",
        ['è'] = "e",  ['é'] = "e",  ['ê'] = "e",  ['ë'] = "e",
        ['Ì'] = "I",  ['Í'] = "I",  ['Î'] = "I",  ['Ï'] = "I",
        ['ì'] = "i",  ['í'] = "i",  ['î'] = "i",  ['ï'] = "i",
        ['Ñ'] = "N",  ['ñ'] = "n",
        ['Ò'] = "O",  ['Ó'] = "O",  ['Ô'] = "O",  ['Õ'] = "O",  ['Ö'] = "O",  ['Ø'] = "O",
        ['ò'] = "o",  ['ó'] = "o",  ['ô'] = "o",  ['õ'] = "o",  ['ö'] = "o",  ['ø'] = "o",
        ['Ù'] = "U",  ['Ú'] = "U",  ['Û'] = "U",  ['Ü'] = "U",
        ['ù'] = "u",  ['ú'] = "u",  ['û'] = "u",  ['ü'] = "u",
        ['Ý'] = "Y",  ['ý'] = "y",  ['ÿ'] = "y",
        ['Æ'] = "Ae", ['æ'] = "ae",
        ['Þ'] = "Th", ['þ'] = "th",
        ['ß'] = "ss",
        // Latin Extended-A block.
        ['Ā'] = "A",  ['Ă'] = "A",  ['Ą'] = "A",
        ['ā'] = "a",  ['ă'] = "a",  ['ą'] = "a",
        ['Ć'] = "C",  ['Ĉ'] = "C",  ['Ċ'] = "C",  ['Č'] = "C",
        ['ć'] = "c",  ['ĉ'] = "c",  ['ċ'] = "c",  ['č'] = "c",
        ['Ď'] = "D",  ['Đ'] = "D",  ['ď'] = "d",  ['đ'] = "d",
        ['Ē'] = "E",  ['Ĕ'] = "E",  ['Ė'] = "E",  ['Ę'] = "E",  ['Ě'] = "E",
        ['ē'] = "e",  ['ĕ'] = "e",  ['ė'] = "e",  ['ę'] = "e",  ['ě'] = "e",
        ['Ĝ'] = "G",  ['Ğ'] = "G",  ['Ġ'] = "G",  ['Ģ'] = "G",
        ['ĝ'] = "g",  ['ğ'] = "g",  ['ġ'] = "g",  ['ģ'] = "g",
        ['Ĥ'] = "H",  ['Ħ'] = "H",  ['ĥ'] = "h",  ['ħ'] = "h",
        ['Ĩ'] = "I",  ['Ī'] = "I",  ['Ĭ'] = "I",  ['Į'] = "I",  ['İ'] = "I",
        ['ĩ'] = "i",  ['ī'] = "i",  ['ĭ'] = "i",  ['į'] = "i",  ['ı'] = "i",
        ['Ĵ'] = "J",  ['ĵ'] = "j",
        ['Ķ'] = "K",  ['ķ'] = "k",  ['ĸ'] = "k",
        ['Ĺ'] = "L",  ['Ļ'] = "L",  ['Ľ'] = "L",  ['Ŀ'] = "L",  ['Ł'] = "L",
        ['ĺ'] = "l",  ['ļ'] = "l",  ['ľ'] = "l",  ['ŀ'] = "l",  ['ł'] = "l",
        ['Ń'] = "N",  ['Ņ'] = "N",  ['Ň'] = "N",  ['Ŋ'] = "N",
        ['ń'] = "n",  ['ņ'] = "n",  ['ň'] = "n",  ['ŋ'] = "n",
        ['Ō'] = "O",  ['Ŏ'] = "O",  ['Ő'] = "O",
        ['ō'] = "o",  ['ŏ'] = "o",  ['ő'] = "o",
        ['Ŕ'] = "R",  ['Ŗ'] = "R",  ['Ř'] = "R",
        ['ŕ'] = "r",  ['ŗ'] = "r",  ['ř'] = "r",
        ['Ś'] = "S",  ['Ŝ'] = "S",  ['Ş'] = "S",  ['Š'] = "S",
        ['ś'] = "s",  ['ŝ'] = "s",  ['ş'] = "s",  ['š'] = "s",
        ['Ţ'] = "T",  ['Ť'] = "T",  ['Ŧ'] = "T",
        ['ţ'] = "t",  ['ť'] = "t",  ['ŧ'] = "t",
        ['Ũ'] = "U",  ['Ū'] = "U",  ['Ŭ'] = "U",  ['Ů'] = "U",  ['Ű'] = "U",  ['Ų'] = "U",
        ['ũ'] = "u",  ['ū'] = "u",  ['ŭ'] = "u",  ['ů'] = "u",  ['ű'] = "u",  ['ų'] = "u",
        ['Ŵ'] = "W",  ['ŵ'] = "w",
        ['Ŷ'] = "Y",  ['ŷ'] = "y",  ['Ÿ'] = "Y",
        ['Ź'] = "Z",  ['Ż'] = "Z",  ['Ž'] = "Z",
        ['ź'] = "z",  ['ż'] = "z",  ['ž'] = "z",
        ['Ĳ'] = "IJ", ['ĳ'] = "ij",
        ['Œ'] = "Oe", ['œ'] = "oe",
        ['ŉ'] = "'n", ['ſ'] = "s",
    };

    static string Deburr(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var deburred = ReLatin.Replace(text, m => DeburredLetters[m.Value[0]]);
        return ReComboMark.Replace(deburred, "");
    }

    static List<string> Words(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }
        var re = ReHasUnicodeWord.IsMatch(text) ? ReUnicodeWord : ReAsciiWord;
        return re.Matches(text).Select(m => m.Value).ToList();
    }

    public static string CamelCase(this string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var cleaned = ReApos.Replace(Deburr(text), string.Empty);
        var words = Words(cleaned);
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var result = words[0].ToLowerInvariant();
        for (var i = 1; i < words.Count; i++)
        {
            var word = words[i].ToLowerInvariant();
            result += char.ToUpperInvariant(word[0]) + word[1..];
        }
        return result;
    }
}
