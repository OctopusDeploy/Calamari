using System;

namespace Calamari.Common.Features.Scripting
{
    public class MonoVersionCanNotBeDeterminedException : Exception
    {
        public MonoVersionCanNotBeDeterminedException(string message)
            : base(message)
        {
        }
    }
}