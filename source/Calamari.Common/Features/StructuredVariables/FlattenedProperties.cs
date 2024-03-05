using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Common.Features.StructuredVariables
{
    public class FlattenedProperties : Dictionary<string, string>
    {
        private string key = "";
        readonly Stack<string> path = new Stack<string>();
        
        public FlattenedProperties(object obj)
        {
            MapToken(JToken.FromObject(obj));
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
        
        void MapObject(JToken j)
        {
            foreach (var property in j.Children<JProperty>())
                MapProperty(property);
        }
        
        void MapArray(JContainer array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                PushPath(index.ToString());
                MapToken(array[index]);
                PopPath();
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
            this[key] = value.Value<string?>() ?? "";
        }

        void MapNumber(JToken value)
        {
            this[key] = value.Value<long>().ToString();
        }

        void MapBool(JToken value)
        {
            this[key] = value.Value<bool>().ToString();
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
    }
}