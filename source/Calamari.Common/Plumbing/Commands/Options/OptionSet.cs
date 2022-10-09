//
// Options.cs
//
// Authors:
//  Jonathan Pryor <jpryor@novell.com>
//
// Copyright (C) 2008 Novell (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Compile With:
//   gmcs -debug+ -r:System.Core Options.cs -o:NDesk.Options.dll
//   gmcs -debug+ -d:LINQ -r:System.Core Options.cs -o:NDesk.Options.dll
//
// The LINQ version just changes the implementation of
// OptionSet.Parse(IEnumerable<string>), and confers no semantic changes.

//
// A Getopt::Long-inspired option parsing library for C#.
//
// NDesk.Options.OptionSet is built upon a key/value table, where the
// key is a option format string and the value is a delegate that is
// invoked when the format string is matched.
//
// Option format strings:
//  Regex-like BNF Grammar:
//    name: .+
//    type: [=:]
//    sep: ( [^{}]+ | '{' .+ '}' )?
//    aliases: ( name type sep ) ( '|' name type sep )*
//
// Each '|'-delimited name is an alias for the associated action.  If the
// format string ends in a '=', it has a required value.  If the format
// string ends in a ':', it has an optional value.  If neither '=' or ':'
// is present, no value is supported.  `=' or `:' need only be defined on one
// alias, but if they are provided on more than one they must be consistent.
//
// Each alias portion may also end with a "key/value separator", which is used
// to split option values if the option accepts > 1 value.  If not specified,
// it defaults to '=' and ':'.  If specified, it can be any character except
// '{' and '}' OR the *string* between '{' and '}'.  If no separator should be
// used (i.e. the separate values should be distinct arguments), then "{}"
// should be used as the separator.
//
// Options are extracted either from the current option by looking for
// the option name followed by an '=' or ':', or is taken from the
// following option IFF:
//  - The current option does not contain a '=' or a ':'
//  - The current option requires a value (i.e. not a Option type of ':')
//
// The `name' used in the option format string does NOT include any leading
// option indicator, such as '-', '--', or '/'.  All three of these are
// permitted/required on any named option.
//
// Option bundling is permitted so long as:
//   - '-' is used to start the option group
//   - all of the bundled options are a single character
//   - at most one of the bundled options accepts a value, and the value
//     provided starts from the next character to the end of the string.
//
// This allows specifying '-a -b -c' as '-abc', and specifying '-D name=value'
// as '-Dname=value'.
//
// Option processing is disabled by specifying "--".  All options after "--"
// are returned by OptionSet.Parse() unchanged and unprocessed.
//
// Unprocessed options are returned from OptionSet.Parse().
//
// Examples:
//  int verbose = 0;
//  OptionSet p = new OptionSet ()
//    .Add ("v", v => ++verbose)
//    .Add ("name=|value=", v => Console.WriteLine (v));
//  p.Parse (new string[]{"-v", "--v", "/v", "-name=A", "/name", "B", "extra"});
//
// The above would parse the argument string array, and would invoke the
// lambda expression three times, setting `verbose' to 3 when complete.
// It would also print out "A" and "B" to standard output.
// The returned array would contain the string "extra".
//
// C# 3.0 collection initializers are supported and encouraged:
//  var p = new OptionSet () {
//    { "h|?|help", v => ShowHelp () },
//  };
//
// System.ComponentModel.TypeConverter is also supported, allowing the use of
// custom data types in the callback type; TypeConverter.ConvertFromString()
// is used to convert the value option to an instance of the specified
// type:
//
//  var p = new OptionSet () {
//    { "foo=", (Foo f) => Console.WriteLine (f.ToString ()) },
//  };
//
// Random other tidbits:
//  - Boolean options (those w/o '=' or ':' in the option format string)
//    are explicitly enabled if they are followed with '+', and explicitly
//    disabled if they are followed with '-':
//      string a = null;
//      var p = new OptionSet () {
//        { "a", s => a = s },
//      };
//      p.Parse (new string[]{"-a"});   // sets v != null
//      p.Parse (new string[]{"-a+"});  // sets v != null
//      p.Parse (new string[]{"-a-"});  // sets v == null
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Calamari.Common.Plumbing.Commands.Options
{
    public delegate TOutput Converter<in TInput, out TOutput>(TInput input);

    public class OptionSet : KeyedCollection<string, Option>
    {
        const int OptionWidth = 29;

        readonly Regex ValueOption = new Regex(
            @"^(?<flag>--|-|/)(?<name>[^:=]+)((?<sep>[:=])(?<value>.*))?$");

        Action<string[]>? leftovers;

#pragma warning disable 649
#pragma warning restore 649

        public bool ShouldWaitForExit { get; }

        protected override string GetKeyForItem(Option item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            if (item.Names != null && item.Names.Length > 0)
                return item.Names[0];
            // This should never happen, as it's invalid for Option to be
            // constructed w/o any names.
            throw new InvalidOperationException("Option has no names!");
        }

        [Obsolete("Use KeyedCollection.this[string]")]
        protected Option? GetOptionForName(string option)
        {
            if (option == null)
                throw new ArgumentNullException("option");
            try
            {
                return base[option];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        protected override void InsertItem(int index, Option item)
        {
            base.InsertItem(index, item);
            AddImpl(item);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            var p = Items[index];
            // KeyedCollection.RemoveItem() handles the 0th item
            for (var i = 1; i < p.Names.Length; ++i)
                Dictionary.Remove(p.Names[i]);
        }

        protected override void SetItem(int index, Option item)
        {
            base.SetItem(index, item);
            RemoveItem(index);
            AddImpl(item);
        }

        void AddImpl(Option option)
        {
            if (option == null)
                throw new ArgumentNullException("option");
            var added = new List<string>(option.Names.Length);
            try
            {
                // KeyedCollection.InsertItem/SetItem handle the 0th name.
                for (var i = 1; i < option.Names.Length; ++i)
                {
                    Dictionary.Add(option.Names[i], option);
                    added.Add(option.Names[i]);
                }
            }
            catch (Exception)
            {
                foreach (var name in added)
                    Dictionary.Remove(name);
                throw;
            }
        }

        public new OptionSet Add(Option option)
        {
            base.Add(option);
            return this;
        }

        public OptionSet Add(string prototype, Action<string>? action)
        {
            return Add(prototype, null, action);
        }

        public OptionSet Add(string prototype, string? description, Action<string>? action)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            Option p = new ActionOption(prototype,
                description,
                1,
                delegate(OptionValueCollection v)
                {
                    action(v[0]);
                });
            base.Add(p);
            return this;
        }

        public OptionSet Add(string prototype, OptionAction<string, string>? action)
        {
            return Add(prototype, null, action);
        }

        public OptionSet Add(string prototype, string? description, OptionAction<string, string>? action)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            Option p = new ActionOption(prototype,
                description,
                2,
                delegate(OptionValueCollection v)
                {
                    action(v[0], v[1]);
                });
            base.Add(p);
            return this;
        }

        public OptionSet Add<T>(string prototype, Action<T> action)
        {
            return Add(prototype, null, action);
        }

        public OptionSet Add<T>(string prototype, string? description, Action<T> action)
        {
            return Add(new ActionOption<T>(prototype, description, action));
        }

        public OptionSet Add<TKey, TValue>(string prototype, OptionAction<TKey, TValue> action)
        {
            return Add(prototype, null, action);
        }

        public OptionSet Add<TKey, TValue>(string prototype, string? description, OptionAction<TKey, TValue> action)
        {
            return Add(new ActionOption<TKey, TValue>(prototype, description, action));
        }

        protected virtual OptionContext CreateOptionContext()
        {
            return new OptionContext(this);
        }

        public OptionSet WithExtras(Action<string[]> lo)
        {
            leftovers = lo;
            return this;
        }

        public List<string> Parse(IEnumerable<string> arguments)
        {
            var process = true;
            var c = CreateOptionContext();
            c.OptionIndex = -1;
#pragma warning disable 618
            var def = GetOptionForName("<>");
#pragma warning restore 618
            var unprocessed =
                from argument in arguments
                where ++c.OptionIndex >= 0 && (process || def != null)
                    ? process
                        ? argument == "--"
                            ? process = false
                            : !Parse(argument, c)
                                ? def != null
                                    ? Unprocessed(def, c, argument)
                                    : true
                                : false
                        : def != null
                            ? Unprocessed(def, c, argument)
                            : true
                    : true
                select argument;
            var r = unprocessed.ToList();

            c.Option?.Invoke(c);

            if (leftovers != null && r.Count > 0)
                leftovers(r.ToArray());

            return r;
        }

        static bool Unprocessed(Option? def, OptionContext c, string argument)
        {
            if (def == null)
                return false;

            c.OptionValues.Add(argument);
            c.Option = def;
            c.Option.Invoke(c);
            return false;
        }

        protected bool GetOptionParts(string argument,
            [NotNullWhen(true)]out string? flag,
            [NotNullWhen(true)]out string? name,
            out string? sep,
            out string? value)
        {
            if (argument == null)
                throw new ArgumentNullException("argument");

            flag = name = sep = value = null;
            var m = ValueOption.Match(argument);
            if (!m.Success)
                return false;
            flag = m.Groups["flag"].Value;
            name = m.Groups["name"].Value;
            if (m.Groups["sep"].Success && m.Groups["value"].Success)
            {
                sep = m.Groups["sep"].Value;
                value = m.Groups["value"].Value;
            }

            return true;
        }

        protected virtual bool Parse(string argument, OptionContext c)
        {
            if (c.Option != null)
            {
                ParseValue(argument, c);
                return true;
            }

            string? f, n, s, v;
            if (!GetOptionParts(argument,
                out f,
                out n,
                out s,
                out v))
                return false;

            var p = this.FirstOrDefault(x => x.Names.Any(y => string.Equals(y, n, StringComparison.OrdinalIgnoreCase)));
            if (p != null)
            {
                c.OptionName = f + n;
                c.Option = p;
                switch (p.OptionValueType)
                {
                    case OptionValueType.None:
                        c.OptionValues.Add(n);
                        c.Option.Invoke(c);
                        break;
                    case OptionValueType.Optional:
                    case OptionValueType.Required:
                        ParseValue(v, c);
                        break;
                }

                return true;
            }

            // no match; is it a bool option?
            if (ParseBool(argument, n, c))
                return true;
            // is it a bundled option?
// ReSharper disable once PossiblyMistakenUseOfParamsMethod
            if (ParseBundledValue(f, string.Concat(n + s + v), c))
                return true;

            return false;
        }

        void ParseValue(string? option, OptionContext c)
        {
            if (option != null)
                foreach (var o in c.Option?.ValueSeparators != null
                    ? option.Split(c.Option.ValueSeparators, StringSplitOptions.None)
                    : new[] { option })
                    c.OptionValues.Add(o);
            if (c.OptionValues.Count == c.Option?.MaxValueCount ||
                c.Option?.OptionValueType == OptionValueType.Optional)
                c.Option.Invoke(c);
            else if (c.OptionValues.Count > c.Option?.MaxValueCount)
                throw new OptionException(string.Format(
                        "Error: Found {0} option values when expecting {1}.",
                        c.OptionValues.Count,
                        c.Option.MaxValueCount),
                    c.OptionName ?? "UnknownOptionName");
        }

        bool ParseBool(string option, string n, OptionContext c)
        {
            Option p;
            string rn;
            if (n.Length >= 1 &&
                (n[n.Length - 1] == '+' || n[n.Length - 1] == '-') &&
                Contains(rn = n.Substring(0, n.Length - 1)))
            {
                p = this[rn];
                var v = n[n.Length - 1] == '+' ? option : null;
                c.OptionName = option;
                c.Option = p;
                if (v != null)
                    c.OptionValues.Add(v);
                p.Invoke(c);
                return true;
            }

            return false;
        }

        bool ParseBundledValue(string f, string n, OptionContext c)
        {
            if (f != "-")
                return false;
            for (var i = 0; i < n.Length; ++i)
            {
                Option p;
                var opt = f + n[i];
                var rn = n[i].ToString();
                if (!Contains(rn))
                {
                    if (i == 0)
                        return false;
                    throw new OptionException(string.Format("Cannot bundle unregistered option '{0}'.", opt), opt);
                }

                p = this[rn];
                switch (p.OptionValueType)
                {
                    case OptionValueType.None:
                        Invoke(c, opt, n, p);
                        break;
                    case OptionValueType.Optional:
                    case OptionValueType.Required:
                    {
                        var v = n.Substring(i + 1);
                        c.Option = p;
                        c.OptionName = opt;
                        ParseValue(v.Length != 0 ? v : null, c);
                        return true;
                    }
                    default:
                        throw new InvalidOperationException("Unknown OptionValueType: " + p.OptionValueType);
                }
            }

            return true;
        }

        static void Invoke(OptionContext c, string name, string value, Option option)
        {
            c.OptionName = name;
            c.Option = option;
            c.OptionValues.Add(value);
            option.Invoke(c);
        }

        public void WriteOptionDescriptions(TextWriter o)
        {
            foreach (var p in this)
            {
                var written = 0;
                if (!WriteOptionPrototype(o, p, ref written))
                    continue;

                if (written < OptionWidth)
                {
                    o.Write(new string(' ', OptionWidth - written));
                }
                else
                {
                    o.WriteLine();
                    o.Write(new string(' ', OptionWidth));
                }

                var lines = GetLines(GetDescription(p.Description));
                o.WriteLine(lines[0]);
                var prefix = new string(' ', OptionWidth);
                for (var i = 1; i < lines.Count; ++i)
                {
                    o.Write(prefix);
                    o.WriteLine(lines[i]);
                }
            }
        }

        bool WriteOptionPrototype(TextWriter o, Option p, ref int written)
        {
            var names = p.Names;

            var i = GetNextOptionIndex(names, 0);
            if (i == names.Length)
                return false;

            if (names[i].Length == 1)
            {
                Write(o, ref written, "  -");
                Write(o, ref written, names[0]);
            }
            else
            {
                Write(o, ref written, "      --");
                Write(o, ref written, names[0]);
            }

            for (i = GetNextOptionIndex(names, i + 1);
                i < names.Length;
                i = GetNextOptionIndex(names, i + 1))
            {
                Write(o, ref written, ", ");
                Write(o, ref written, names[i].Length == 1 ? "-" : "--");
                Write(o, ref written, names[i]);
            }

            if (p.OptionValueType == OptionValueType.Optional ||
                p.OptionValueType == OptionValueType.Required)
            {
                if (p.OptionValueType == OptionValueType.Optional)
                    Write(o, ref written, "[");
                Write(o, ref written, "=" + GetArgumentName(0, p.MaxValueCount, p.Description));
                var sep = p.ValueSeparators != null && p.ValueSeparators.Length > 0
                    ? p.ValueSeparators[0]
                    : " ";
                for (var c = 1; c < p.MaxValueCount; ++c)
                    Write(o, ref written, sep + GetArgumentName(c, p.MaxValueCount, p.Description));
                if (p.OptionValueType == OptionValueType.Optional)
                    Write(o, ref written, "]");
            }

            return true;
        }

        static int GetNextOptionIndex(string[] names, int i)
        {
            while (i < names.Length && names[i] == "<>")
                ++i;
            return i;
        }

        static void Write(TextWriter o, ref int n, string s)
        {
            n += s.Length;
            o.Write(s);
        }

        static string GetArgumentName(int index, int maxIndex, string? description)
        {
            if (description == null)
                return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
            string[] nameStart;
            if (maxIndex == 1)
                nameStart = new[] { "{0:", "{" };
            else
                nameStart = new[] { "{" + index + ":" };
            for (var i = 0; i < nameStart.Length; ++i)
            {
                int start, j = 0;
                do
                {
                    start = description.IndexOf(nameStart[i], j, StringComparison.Ordinal);
                } while (start >= 0 && j != 0 && description[j++ - 1] == '{');

                if (start == -1)
                    continue;
                var end = description.IndexOf("}", start, StringComparison.Ordinal);
                if (end == -1)
                    continue;
                return description.Substring(start + nameStart[i].Length, end - start - nameStart[i].Length);
            }

            return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
        }

        static string GetDescription(string? description)
        {
            if (description == null)
                return string.Empty;
            var sb = new StringBuilder(description.Length);
            var start = -1;
            for (var i = 0; i < description.Length; ++i)
                switch (description[i])
                {
                    case '{':
                        if (i == start)
                        {
                            sb.Append('{');
                            start = -1;
                        }
                        else if (start < 0)
                        {
                            start = i + 1;
                        }

                        break;
                    case '}':
                        if (start < 0)
                        {
                            if (i + 1 == description.Length || description[i + 1] != '}')
                                throw new InvalidOperationException("Invalid option description: " + description);
                            ++i;
                            sb.Append("}");
                        }
                        else
                        {
                            sb.Append(description.Substring(start, i - start));
                            start = -1;
                        }

                        break;
                    case ':':
                        if (start < 0)
                            goto default;
                        start = i + 1;
                        break;
                    default:
                        if (start < 0)
                            sb.Append(description[i]);
                        break;
                }

            return sb.ToString();
        }

        static List<string> GetLines(string description)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(description))
            {
                lines.Add(string.Empty);
                return lines;
            }

            var length = 80 - OptionWidth - 2;
            int start = 0, end;
            do
            {
                end = GetLineEnd(start, length, description);
                var cont = false;
                if (end < description.Length)
                {
                    var c = description[end];
                    if (c == '-' || char.IsWhiteSpace(c) && c != '\n')
                    {
                        ++end;
                    }
                    else if (c != '\n')
                    {
                        cont = true;
                        --end;
                    }
                }

                lines.Add(description.Substring(start, end - start));
                if (cont)
                    lines[lines.Count - 1] += "-";
                start = end;
                if (start < description.Length && description[start] == '\n')
                    ++start;
            } while (end < description.Length);

            return lines;
        }

        static int GetLineEnd(int start, int length, string description)
        {
            var end = Math.Min(start + length, description.Length);
            var sep = -1;
            for (var i = start; i < end; ++i)
                switch (description[i])
                {
                    case ' ':
                    case '\t':
                    case '\v':
                    case '-':
                    case ',':
                    case '.':
                    case ';':
                        sep = i;
                        break;
                    case '\n':
                        return i;
                }

            if (sep == -1 || end == description.Length)
                return end;
            return sep;
        }

        sealed class ActionOption : Option
        {
            readonly Action<OptionValueCollection> action;

            public ActionOption(string prototype, string? description, int count, Action<OptionValueCollection>? action)
                : base(prototype, description, count)
            {
                if (action == null)
                    throw new ArgumentNullException("action");
                this.action = action;
            }

            protected override void OnParseComplete(OptionContext c)
            {
                action(c.OptionValues);
            }
        }

        sealed class ActionOption<T> : Option
        {
            readonly Action<T> action;

            public ActionOption(string prototype, string? description, Action<T>? action)
                : base(prototype, description, 1)
            {
                if (action == null)
                    throw new ArgumentNullException("action");
                this.action = action;
            }

            protected override void OnParseComplete(OptionContext c)
            {
                action(Parse<T>(c.OptionValues[0], c));
            }
        }

        sealed class ActionOption<TKey, TValue> : Option
        {
            readonly OptionAction<TKey, TValue> action;

            public ActionOption(string prototype, string? description, OptionAction<TKey, TValue>? action)
                : base(prototype, description, 2)
            {
                if (action == null)
                    throw new ArgumentNullException("action");
                this.action = action;
            }

            protected override void OnParseComplete(OptionContext c)
            {
                action(
                    Parse<TKey>(c.OptionValues[0], c),
                    Parse<TValue>(c.OptionValues[1], c));
            }
        }
    }
}