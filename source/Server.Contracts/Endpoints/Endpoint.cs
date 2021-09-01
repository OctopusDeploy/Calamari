using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Endpoints
{
    public abstract class Endpoint
    {
        [JsonIgnore]
        public abstract DeploymentTargetType DeploymentTargetType { get; }

        [JsonIgnore]
        public abstract string Description { get; }

        [JsonIgnore]
        public virtual bool ScriptConsoleSupported => false;

        [JsonIgnore]
        public bool AlwaysRequiresAWorker => this is IRunsOnAWorker;

        public abstract IEnumerable<(string id, DocumentType documentType)> GetRelatedDocuments();

        public abstract IEnumerable<Variable> ContributeVariables();
    }
}