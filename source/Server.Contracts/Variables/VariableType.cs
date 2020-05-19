﻿using System;

namespace Sashimi.Server.Contracts.Variables
{
    public class VariableType
    {
        public static readonly VariableType String = new VariableType("String");
        public static readonly VariableType Sensitive = new VariableType("Sensitive");
        public static readonly VariableType Certificate = new VariableType("Certificate", Contracts.DocumentType.Certificate);
        public static readonly VariableType WorkerPool = new VariableType("WorkerPool", Contracts.DocumentType.WorkerPool);
        public static readonly VariableType AmazonWebServicesAccount = new VariableType("AmazonWebServicesAccount", Contracts.DocumentType.Account);
        public static readonly VariableType AzureAccount = new VariableType("AzureAccount", Contracts.DocumentType.Account);

        public VariableType(string name, DocumentType? documentType = null)
        {
            Name = name;
            DocumentType = documentType;
        }

        public string Name { get; }
        public DocumentType? DocumentType { get; }

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
            => (Name != null ? Name.GetHashCode() : 0);

        public static bool operator ==(VariableType left, VariableType right) 
            => Equals(left, right);

        public static bool operator !=(VariableType left, VariableType right) 
            => !Equals(left, right);
    }
}