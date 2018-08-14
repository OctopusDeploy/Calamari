using System.Collections.Generic;

namespace Calamari.Shared.Util
{
    public interface ITemplate
    {
        string Content { get; }
    }
    
    public interface ITemplateInputs<TInput>
    {
        IEnumerable<TInput> Inputs { get; }
    }
    
    public interface ITemplateOutputs<TOutput>
    {
        bool HasOutputs { get; }
        IEnumerable<TOutput> Outputs { get; }
    }
}