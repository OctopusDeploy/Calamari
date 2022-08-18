using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Amazon.Runtime;

namespace Calamari.Aws.Util
{
    public static class ConstantHelpers
    {
        public static IEnumerable<T> GetConstantValues<T>() where T : ConstantClass
        {
            var type = typeof(T);
            return type.GetFields(BindingFlags.Public | BindingFlags.GetField | BindingFlags.Static)
                .Where(x => type.IsAssignableFrom(x.FieldType)).Select(x => (T)x.GetValue(null));
        }
    }
}
