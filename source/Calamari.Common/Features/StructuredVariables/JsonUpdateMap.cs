using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;

namespace Calamari.Common.Features.StructuredVariables
{
    public class JsonUpdateMap
    {
        readonly ILog log;
        readonly IDictionary<string, Action<string?>> map = new SortedDictionary<string, Action<string?>>(StringComparer.OrdinalIgnoreCase);
        readonly Stack<string> path = new Stack<string>();
        string? key;

        public JsonUpdateMap(ILog log)
        {
            this.log = log;
        }

        public void Load(JToken json)
        {
            if (json.Type == JTokenType.Array)
                MapArray(json.Value<JArray>(), true);
            else
                MapObject(json, true);
        }

        void MapObject(JToken j, bool first = false)
        {
            if (!first)
            {
                if (key == null)
                    throw new InvalidOperationException("Path has not been pushed");
            
                map[key] = t => j.Replace(JToken.Parse(t));
            }

            foreach (var property in j.Children<JProperty>())
                MapProperty(property);
        }

        void MapToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    MapObject(token.Value<JObject>());
                    break;

                case JTokenType.Array:
                    MapArray(token.Value<JArray>());
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                    MapNumber(token);
                    break;

                case JTokenType.Boolean:
                    MapBool(token);
                    break;

                default:
                    MapDefault(token);
                    break;
            }
        }

        void MapProperty(JProperty property)
        {
            PushPath(property.Name);
            MapToken(property.Value);
            PopPath();
        }

        void MapDefault(JToken value)
        {
            if (key == null)
                throw new InvalidOperationException("Path has not been pushed");
            map[key] = t => value.Replace(t == null ? null : JToken.FromObject(t));
        }

        void MapNumber(JToken value)
        {
            if (key == null)
                throw new InvalidOperationException("Path has not been pushed");
            map[key] = t =>
                       {
                           long longvalue;
                           if (long.TryParse(t, out longvalue))
                           {
                               value.Replace(JToken.FromObject(longvalue));
                               return;
                           }

                           double doublevalue;
                           if (double.TryParse(t, out doublevalue))
                           {
                               value.Replace(JToken.FromObject(doublevalue));
                               return;
                           }

                           value.Replace(JToken.FromObject(t));
                       };
        }

        void MapBool(JToken value)
        {
            if (key == null)
                throw new InvalidOperationException("Path has not been pushed");
            map[key] = t =>
                       {
                           bool boolvalue;
                           if (bool.TryParse(t, out boolvalue))
                           {
                               value.Replace(JToken.FromObject(boolvalue));
                               return;
                           }

                           value.Replace(JToken.FromObject(t));
                       };
        }

        void MapArray(JContainer array, bool first = false)
        {
            if (!first)
            {
                if (key == null)
                    throw new InvalidOperationException("Path has not been pushed");
            
                map[key] = t => array.Replace(JToken.Parse(t));
            }

            for (var index = 0; index < array.Count; index++)
            {
                PushPath(index.ToString());
                MapToken(array[index]);
                PopPath();
            }
        }

        void PushPath(string p)
        {
            path.Push(p);
            key = string.Join(":", path.Reverse());
        }

        void PopPath()
        {
            path.Pop();
            key = string.Join(":", path.Reverse());
        }

        public void Update(IVariables variables)
        {
            bool VariableNameIsMappedPath(string v)
            {
                if (v.StartsWith("Octopus", StringComparison.OrdinalIgnoreCase)
                    && !v.StartsWith("Octopus:", StringComparison.OrdinalIgnoreCase))
                    // Only include variables starting with 'Octopus'
                    // if it also has a colon (:)
                    return false;
                return map.ContainsKey(v);
            }

            var replaced = 0;
            foreach (var name in variables.GetNames().Where(VariableNameIsMappedPath))
                try
                {
                    log.Verbose(StructuredConfigMessages.StructureFound(name));
                    replaced++;
                    map[name](variables.Get(name));
                }
                catch (Exception e)
                {
                    log.WarnFormat("Unable to set value for {0}. The following error occurred: {1}", name, e.Message);
                }

            if (replaced == 0)
                log.Info(StructuredConfigMessages.NoStructuresFound);
        }
    }
}