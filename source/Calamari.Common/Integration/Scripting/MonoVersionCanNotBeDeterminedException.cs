using System;

namespace Calamari.Common.Integration.Scripting
{
    public class MonoVersionCanNotBeDeterminedException : Exception
    {
        public MonoVersionCanNotBeDeterminedException(string message) : base(message)
        {
        }
    }
}