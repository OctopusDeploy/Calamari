using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Calamari.Common.Features.StructuredVariables
{
    public class YamlTagResolver
    {
        public const string TagUriMap = @"tag:yaml.org,2002:map";
        public const string TagUriSeq = @"tag:yaml.org,2002:seq";
        public const string TagUriStr = @"tag:yaml.org,2002:str";
        public const string TagUriNull = @"tag:yaml.org,2002:null";
        public const string TagUriBool = @"tag:yaml.org,2002:bool";
        public const string TagUriInt = @"tag:yaml.org,2002:int";
        public const string TagUriFloat = @"tag:yaml.org,2002:float";

        public static string ResolveTag(Scalar scalar)
        {
            if (!string.IsNullOrWhiteSpace(scalar.Tag))
                return scalar.Tag;

            if (TryDeserialize<NullNodeDeserializer, object>(scalar).Succeeded)
                return TagUriNull;
            if (TryDeserialize<ScalarNodeDeserializer, bool>(scalar).Succeeded)
                return TagUriBool;
            if (TryDeserialize<ScalarNodeDeserializer, int>(scalar).Succeeded)
                return TagUriInt;
            if (TryDeserialize<ScalarNodeDeserializer, float>(scalar).Succeeded)
                return TagUriFloat;
            return TagUriStr;
        }

        public static bool SuitsTag(Scalar scalar, string tag)
        {
            switch (tag)
            {
                case TagUriNull: return TryDeserialize<NullNodeDeserializer, object>(scalar).Succeeded;
                case TagUriBool: return TryDeserialize<ScalarNodeDeserializer, bool>(scalar).Succeeded;
                case TagUriInt: return TryDeserialize<ScalarNodeDeserializer, int>(scalar).Succeeded;
                case TagUriFloat: return TryDeserialize<ScalarNodeDeserializer, float>(scalar).Succeeded;
                case TagUriStr: return true;
                default: return false;
            }
        }

        public static (bool Succeeded, T Value) TryDeserialize<TNodeDeserializer, T>(Scalar scalar)
            where TNodeDeserializer : INodeDeserializer, new()
        {
            INodeDeserializer deserializer = new TNodeDeserializer();
            var parser = new SingleEventParser(scalar);
            try
            {
                if (deserializer.Deserialize(parser, typeof(T), null, out var value))
                    return (true, (T)value);
                return (false, default);
            }
            catch
            {
                return (false, default);
            }
        }

        class SingleEventParser : IParser
        {
            public SingleEventParser(ParsingEvent current)
            {
                Current = current;
            }

            public ParsingEvent Current { get; }

            public bool MoveNext()
            {
                return false;
            }
        }
    }
}