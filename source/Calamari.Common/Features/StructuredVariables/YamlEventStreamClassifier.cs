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
        public YamlStructure Type { get; }
        public string MappingKey { get; set; }
        public int SequenceIndex { get; set; } = -1;

        public YamlPathComponent(YamlStructure structure)
        {
            Type = structure;
        }
    }

    class YamlPathStack
    {
        readonly Stack<YamlPathComponent> stack = new Stack<YamlPathComponent>();

        public void Push(YamlStructure structure) => stack.Push(new YamlPathComponent(structure));

        public void Pop() => stack.Pop();

        IEnumerable<string> GetPathComponents()
        {
            foreach (var stackItem in stack.Reverse())
            {
                if (stackItem.MappingKey != null)
                    yield return stackItem.MappingKey;
                else if (stackItem.Type == YamlStructure.Sequence)
                    yield return stackItem.SequenceIndex.ToString();
            }
        }

        public string GetPath() => string.Join(":", GetPathComponents());

        public bool TopIsSequence => stack.Count > 0
                                     && stack.Peek().Type == YamlStructure.Sequence;

        public void TopSequenceIncrementIndex()
        {
            if (TopIsSequence)
                stack.Peek().SequenceIndex++;
        }

        public bool TopIsMappingExpectingKey => stack.Count > 0
                                                && stack.Peek().Type == YamlStructure.Mapping
                                                && stack.Peek().MappingKey == null;

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

    public interface IYamlNode
    {
        ParsingEvent ParsingEvent { get; }
        string Path { get; }
    }

    public class YamlNode<T> : IYamlNode where T : ParsingEvent
    {
        public T ParsingEvent { get; }
        public string Path { get; }

        public YamlNode(T parsingEvent, string path)
        {
            ParsingEvent = parsingEvent;
            Path = path;
        }

        ParsingEvent IYamlNode.ParsingEvent => ParsingEvent;
    }

    public class YamlScalarValueNode : YamlNode<Scalar>
    {
        public string Value { get; }

        public YamlScalarValueNode(Scalar scalar, string path, string value)
            : base(scalar, path)
        {
            Value = value;
        }

        public Scalar ReplaceValue(string newValue)
        {
            return new Scalar(ParsingEvent.Anchor, ParsingEvent.Tag, newValue, ParsingEvent.Style,
                ParsingEvent.IsPlainImplicit, ParsingEvent.IsQuotedImplicit, ParsingEvent.Start, ParsingEvent.End);
        }
    }

    public class YamlEventStreamClassifier
    {
        readonly YamlPathStack stack = new YamlPathStack();

        public IYamlNode Process(ParsingEvent ev)
        {
            IYamlNode classifiedNode = null;

            if (stack.TopIsSequence && (ev is MappingStart || ev is SequenceStart || ev is Scalar))
            {
                stack.TopSequenceIncrementIndex();
            }

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
                        classifiedNode = new YamlScalarValueNode(sc, stack.GetPath(), sc.Value);
                        stack.TopMappingKeyEnd();
                    }

                    break;
            }

            return classifiedNode ?? new YamlNode<ParsingEvent>(ev, stack.GetPath());
        }
    }
}