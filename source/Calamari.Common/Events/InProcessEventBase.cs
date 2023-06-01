using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Common.Events
{
    public abstract class InProcessEventBase<T>
    {
        private readonly object @lock = new object();
        private readonly List<Action<T>> subscribers = new List<Action<T>>();

        public void Subscribe(Action<T> subscription)
        {
            lock (@lock)
            {
                subscribers.Add(subscription);
            }
        }

        public void Publish(T payload)
        {
            var caughtExceptions = new List<Exception>();
            lock (@lock)
            {
                foreach (var subscriber in subscribers)
                {
                    try
                    {
                        subscriber(payload);
                    }
                    catch (Exception e)
                    {
                        caughtExceptions.Add(e);
                    }
                }
            }

            if (caughtExceptions.Any())
            {
                throw new AggregateException("Failed to Publish event to at least 1 subscriber", caughtExceptions);
            }
        }
    }

    public class InProcessEventBase : InProcessEventBase<object>
    {
        public void Subscribe(Action subscription)
        {
            Subscribe(_ => subscription());
        }

        public void Publish()
        {
            Publish(new object());
        }
    }
}