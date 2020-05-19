﻿using System;

namespace Sashimi.Server.Contracts.Variables
{
    public class VariableType
    {
        public static readonly VariableType String = new VariableType("String");
        public static readonly VariableType Sensitive = new VariableType("Sensitive");
        public static readonly VariableType Certificate = new VariableType("Certificate", IsDocumentReference.Yes);
        public static readonly VariableType WorkerPool = new VariableType("WorkerPool", IsDocumentReference.Yes);
        public static readonly VariableType AmazonWebServicesAccount = new VariableType("AmazonWebServicesAccount", IsDocumentReference.Yes);
        public static readonly VariableType AzureAccount = new VariableType("AzureAccount", IsDocumentReference.Yes);

        public VariableType(string name, IsDocumentReference isDocumentReference = IsDocumentReference.No)
        {
            Name = name;
            IsDocumentReference = isDocumentReference;
        }

        public string Name { get; }
        public IsDocumentReference IsDocumentReference { get; }

        protected bool Equals(VariableType other)
        {
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((VariableType) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }

    public enum IsDocumentReference
    {
        No,
        Yes
    }
}