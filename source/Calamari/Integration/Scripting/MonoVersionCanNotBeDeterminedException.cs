using System;

namespace Calamari.Integration.Scripting
{
    public class MonoVersionCanNotBeDeterminedException : Exception
    {
        public MonoVersionCanNotBeDeterminedException(string message) : base(message)
        {
        }
    }
}