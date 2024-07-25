using System;

namespace Calamari.FullFrameworkTools.Command
{
    public abstract class FullFrameworkToolCommandHandler<TFullFrameworkToolRequest, TFullFrameworkToolResponse> : IFullFrameworkToolCommandHandler
        where TFullFrameworkToolRequest : IFullFrameworkToolRequest
        where TFullFrameworkToolResponse : IFullFrameworkToolResponse

    {
        protected abstract TFullFrameworkToolResponse Handle(TFullFrameworkToolRequest request);

        public object Handle(object request)
        {
            return this.Handle((TFullFrameworkToolRequest)request);
        }
    }
    
    public interface IFullFrameworkToolRequest
    {
    }

    public interface IFullFrameworkToolResponse
    {
    }


    public interface IFullFrameworkToolCommandHandler
    {
        object Handle(object request);
    }
}