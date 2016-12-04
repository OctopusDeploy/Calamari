using Calamari.Extensibility;

namespace Calamari.Utilities
{
    public class VariableDictionary : Octostache.VariableDictionary, IVariableDictionary
    {
        public void SetOutputVariable(string name, string value)
        {
            throw new System.NotImplementedException();
        }
    }
}
