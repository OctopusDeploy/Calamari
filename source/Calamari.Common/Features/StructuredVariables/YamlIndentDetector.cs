using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Events;

namespace Calamari.Common.Features.StructuredVariables
{
    public class YamlIndentDetector
    {
        readonly List<int> indents = new List<int>();
        int lastNestingChangeColumn = 1;

        public void Process(ParsingEvent ev)
        {
            if (ev.NestingIncrease > 0
                && (ev is MappingStart ms && ms.Style != MappingStyle.Flow
                    || ev is SequenceStart ss && ss.Style != SequenceStyle.Flow))
            {
                var startColumnChange = ev.Start.Column - lastNestingChangeColumn;
                if (IndentDoesNotCrashYamlDotNetEmitter(startColumnChange))
                    indents.Add(startColumnChange);
                lastNestingChangeColumn = ev.Start.Column;
            }

            if (ev.NestingIncrease < 1 && ev.Start.Column < lastNestingChangeColumn)
                lastNestingChangeColumn = ev.Start.Column;
        }

        static bool IndentDoesNotCrashYamlDotNetEmitter(int indent)
        {
            return indent >= 2 && indent <= 9;
        }

        public int GetMostCommonIndent()
        {
            return indents.GroupBy(indent => indent)
                          .OrderByDescending(group => group.Count())
                          .FirstOrDefault()
                          ?.Key
                   ?? 2;
        }
    }
}