using System;
using System.Threading.Tasks;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class RetentionMiniMediator
    {
    }
    /*
public abstract class MiniMediator
{


public Task RegisterHandler<TEvent>(IHandleEvent<TEvent> handler) where TEvent : IEvent
{

}

public Task RaiseEvent<TEvent>(IEventArguments<TEvent> args) where TEvent : IEvent
{

}
      */
}

public interface IRaiseEvent<TEvent> where TEvent : IEvent
{
    Task RaiseEvent(IEventArguments<TEvent> args);
}

public interface IHandleEvent<TEvent> where TEvent : IEvent
{
    Task Handle(TEvent ev);
}

public interface IEventArguments<TEvent> where TEvent : IEvent
{
}

public interface IEvent
{
}