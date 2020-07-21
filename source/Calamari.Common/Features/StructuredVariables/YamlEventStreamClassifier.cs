using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Core;
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
        public string? MappingKey { get; set; }
        public int SequenceIndex { get; set; } = -1;
    }

    class YamlPathStack
    {
        readonly Stack<YamlPathComponent> stack = new Stack<YamlPathComponent>();

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
                else if (stackItem.Type == YamlStructure.Sequence && stackItem.SequenceIndex != -1)
                    yield return stackItem.SequenceIndex.ToString();
        }

        public string GetPath()
        {
            return string.Join(":", GetPathComponents());
        }

        public bool TopIsSequence()
        {
            return stack.Count > 0
                   && stack.Peek().Type == YamlStructure.Sequence;
        }

        public void TopSequenceIncrementIndex()
        {
            if (TopIsSequence())
                stack.Peek().SequenceIndex++;
        }

        public bool TopIsMappingExpectingKey()
        {
            return stack.Count > 0
                   && stack.Peek().Type == YamlStructure.Mapping
                   && stack.Peek().MappingKey == null;
        }

        public void TopMappingKeyStart(string key)
        {
            if (TopIsMappingExpectingKey())
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
        ParsingEvent Event { get; }
        string Path { get; }
    }

    public class YamlNode<T> : IYamlNode where T : ParsingEvent
    {
        public YamlNode(T parsingEvent, string path)
        {
            Event = parsingEvent;
            Path = path;
        }

        public T Event { get; }
        public string Path { get; }

        ParsingEvent IYamlNode.Event => Event;
    }

    public static class ParsingEventExtensions
    {
        public static Scalar ReplaceValue(this Scalar scalar, string newValue)
        {
            return newValue != null
                ? new Scalar(scalar.Anchor,
                             scalar.Tag,
                             newValue,
                             scalar.Style,
                             scalar.IsPlainImplicit,
                             scalar.IsQuotedImplicit,
                             scalar.Start,
                             scalar.End)
                : new Scalar(scalar.Anchor,
                             "!!null",
                             "null",
                             ScalarStyle.Plain,
                             true,
                             false,
                             scalar.Start,
                             scalar.End);
        }

        public static Scalar ReplaceValueWithString(this Scalar scalar, string newValue)
        {
            return newValue != null
                ? new Scalar(scalar.Anchor,
                             scalar.Tag,
                             newValue,
                             ScalarStyle.SingleQuoted,
                             true,
                             true,
                             scalar.Start,
                             scalar.End)
                : new Scalar(scalar.Anchor,
                             "!!null",
                             "null",
                             ScalarStyle.Plain,
                             true,
                             false,
                             scalar.Start,
                             scalar.End);
        }
    }

    public class YamlEventStreamClassifier
    {
        readonly YamlPathStack stack = new YamlPathStack();

        public IYamlNode Process(ParsingEvent ev)
        {
            IYamlNode? classifiedNode = null;

            if (stack.TopIsSequence() && (ev is MappingStart || ev is SequenceStart || ev is Scalar))
                stack.TopSequenceIncrementIndex();

            switch (ev)
            {
                case MappingStart ms:
                    classifiedNode = new YamlNode<MappingStart>(ms, stack.GetPath());
                    stack.Push(YamlStructure.Mapping);
                    break;
                case MappingEnd me:
                    stack.Pop();
                    classifiedNode = new YamlNode<MappingEnd>(me, stack.GetPath());
                    stack.TopMappingKeyEnd();
                    break;
                case SequenceStart ss:
                    classifiedNode = new YamlNode<SequenceStart>(ss, stack.GetPath());
                    stack.Push(YamlStructure.Sequence);
                    break;
                case SequenceEnd se:
                    stack.Pop();
                    classifiedNode = new YamlNode<SequenceEnd>(se, stack.GetPath());
                    stack.TopMappingKeyEnd();
                    break;
                case Scalar sc:
                    if (stack.TopIsMappingExpectingKey())
                    {
                        // This is a map key
                        stack.TopMappingKeyStart(sc.Value);
                    }
                    else
                    {
                        // This is a value in a map or sequence
                        classifiedNode = new YamlNode<Scalar>(sc, stack.GetPath());
                        stack.TopMappingKeyEnd();
                    }

                    break;
            }

            return classifiedNode ?? new YamlNode<ParsingEvent>(ev, stack.GetPath());
        }
    }
}