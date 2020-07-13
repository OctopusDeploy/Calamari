using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core.Events;

namespace Calamari.Common.Features.StructuredVariables
{
    enum YamlStructure
    {
        Mapping,
        Sequence
    }

    class YamlPathComponent
    {
        public YamlPathComponent(YamlStructure structure)
        {
            Type = structure;
        }

        public YamlStructure Type { get; }
        public string MappingKey { get; set; }
        public int SequenceIndex { get; set; } = -1;
    }

    class YamlPathStack
    {
        readonly Stack<YamlPathComponent> stack = new Stack<YamlPathComponent>();

        public bool TopIsSequence => stack.Count > 0 && stack.Peek().Type == YamlStructure.Sequence;

        public bool TopIsMappingExpectingKey => stack.Count > 0 && stack.Peek().Type == YamlStructure.Mapping && stack.Peek().MappingKey == null;

        public void Push(YamlStructure structure)
        {
            stack.Push(new YamlPathComponent(structure));
        }

        public void Pop()
        {
            stack.Pop();
        }

        IEnumerable<string> GetPathComponents()
        {
            foreach (var stackItem in stack.Reverse())
                if (stackItem.MappingKey != null)
                    yield return stackItem.MappingKey;
                else if (stackItem.Type == YamlStructure.Sequence)
                    yield return stackItem.SequenceIndex.ToString();
        }

        public string GetPath()
        {
            return string.Join(":", GetPathComponents());
        }

        public void TopSequenceIncrementIndex()
        {
            if (TopIsSequence)
                stack.Peek().SequenceIndex++;
        }

        public void TopMappingKeyStart(string key)
        {
            if (TopIsMappingExpectingKey)
                stack.Peek().MappingKey = key;
        }

        public void TopMappingKeyEnd()
        {
            if (stack.Count > 0)
                stack.Peek().MappingKey = null;
        }
    }

    public abstract class YamlNode
    {
        protected YamlNode(IList<ParsingEvent> parsingEvents, string path)
        {
            ParsingEvents = parsingEvents;
            Path = path;
        }

        public IList<ParsingEvent> ParsingEvents { get; }
        public string Path { get; }
    }

    public class YamlScalarValueNode : YamlNode
    {
        readonly Scalar scalar;

        public YamlScalarValueNode(Scalar scalar, string path, string value)
            : base(new List<ParsingEvent>
                {
                    scalar
                },
                path)
        {
            this.scalar = scalar;
            Value = value;
        }

        public string Value { get; }

        public Scalar ReplaceValue(string newValue)
        {
            return new Scalar(scalar.Anchor,
                scalar.Tag,
                newValue,
                scalar.Style,
                scalar.IsPlainImplicit,
                scalar.IsQuotedImplicit,
                scalar.Start,
                scalar.End);
        }
    }

    public class YamlEventStreamClassifier
    {
        readonly YamlPathStack stack = new YamlPathStack();

        public YamlNode Process(ParsingEvent ev)
        {
            YamlNode result = null;

            if (stack.TopIsSequence && (ev is MappingStart || ev is SequenceStart || ev is Scalar))
                stack.TopSequenceIncrementIndex();

            switch (ev)
            {
                case MappingStart _:
                    stack.Push(YamlStructure.Mapping);
                    break;
                case MappingEnd _:
                    stack.Pop();
                    stack.TopMappingKeyEnd();
                    break;
                case SequenceStart _:
                    stack.Push(YamlStructure.Sequence);
                    break;
                case SequenceEnd _:
                    stack.Pop();
                    stack.TopMappingKeyEnd();
                    break;
                case Scalar sc:
                    if (stack.TopIsMappingExpectingKey)
                    {
                        // This is a map key
                        stack.TopMappingKeyStart(sc.Value);
                    }
                    else
                    {
                        // This is a value in a map or sequence
                        result = new YamlScalarValueNode(sc, stack.GetPath(), sc.Value);
                        stack.TopMappingKeyEnd();
                    }

                    break;
            }

            return result;
        }
    }
}