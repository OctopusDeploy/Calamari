using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Common.Util
{
    public interface ITemplate
    {
        string Content { get; }
    }

    public interface ITemplateInputs<TInput>
    {
        IEnumerable<TInput> Inputs { get; }
    }
    
    public class EmptyTemplateInputs<TInput> : ITemplateInputs<TInput>
    {
        public IEnumerable<TInput> Inputs => Enumerable.Empty<TInput>();
    }

    public interface ITemplateOutputs<TOutput>
    {
        bool HasOutputs { get; }
        IEnumerable<TOutput> Outputs { get; }
    }
}