using System;
using System.Runtime.Serialization;

namespace Calamari.Testing.Helpers
{
    /// <summary>
    /// Use this for short-circuiting eventually-consistent assertions. This exception means that this test will *never* succeed, and
    /// that there is no point waiting around, hoping for it to do so.
    /// </summary>
    /// <remarks>
    /// Yes, we know this is an example of using an exception for control flow. We're choosing it to optimize for the common
    /// calling convention (i.e. we don't have an early-exit condition). We can revisit if we end up having more frequent
    /// usages than we expect.
    /// </remarks>
    [Serializable]
    public class EventualAssertionPermanentlyFailedException : Exception
    {
        public EventualAssertionPermanentlyFailedException()
        {
        }

        public EventualAssertionPermanentlyFailedException(string message) : base(message)
        {
        }

        public EventualAssertionPermanentlyFailedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected EventualAssertionPermanentlyFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
