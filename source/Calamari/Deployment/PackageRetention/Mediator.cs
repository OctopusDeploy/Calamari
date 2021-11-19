using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Calamari.Deployment.PackageRetention
{
    public class Mediator : ISimpleMediator
    {
        readonly List<WeakReference> handlers = new List<WeakReference>();

        public void RegisterHandler<TEvent>(IHandleEvent<TEvent> handler) where TEvent : IEvent
        {
            handlers.Add(new WeakReference(handler));
        }

        public void RaiseEvent<TEvent>(IEventArguments<TEvent> args) where TEvent : IEvent
        {
            handlers.RemoveAll(h => !h.IsAlive);

            foreach (var weakReference in handlers)
            {
                if (weakReference.IsAlive && weakReference.Target is IHandleEvent<TEvent> handler)
                {
                    handler.Handle(args);
                }
            }
        }
    }

    public interface ISimpleMediator
    {
        void RegisterHandler<TEvent>(IHandleEvent<TEvent> handler) where TEvent : IEvent;
        void RaiseEvent<TEvent>(IEventArguments<TEvent> args) where TEvent : IEvent;
    }

    public interface IRaiseEvents
    {
        void RaiseEvent<TEvent>(IEventArguments<TEvent> args) where TEvent : IEvent;
    }

    public interface IHandleEvent<TEvent> : IHandleEvent where TEvent : IEvent
    {
        void Handle(IEventArguments<TEvent> args);
    }

    public interface IHandleEvent
    {
    }

    public interface IEventArguments<TEvent> where TEvent : IEvent
    {
    }

    public interface IEvent
    {
    }
}