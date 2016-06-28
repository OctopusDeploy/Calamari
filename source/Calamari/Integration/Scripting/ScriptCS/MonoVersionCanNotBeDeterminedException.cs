using System;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public class MonoVersionCanNotBeDeterminedException : Exception
    {
        public MonoVersionCanNotBeDeterminedException(string message) : base(message)
        {
        }
    }
}