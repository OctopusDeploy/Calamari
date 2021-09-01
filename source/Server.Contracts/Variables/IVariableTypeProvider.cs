using System;

namespace Sashimi.Server.Contracts.Variables
{
    public interface IVariableTypeProvider
    {
        VariableType VariableType { get; }
        DocumentType? DocumentType { get; }
    }
}