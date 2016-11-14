using System;
using System.Collections.Generic;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;

namespace Calamari.Features
{

    public class ConventionThing
    {
        private static readonly Predicate<IVariableDictionary> Unconditional = (v) => true;
        private static readonly Func<IVariableDictionary, IEnumerable<object[]>> NonArgumentative = (v) => new object[][] { new object[0] };

        public readonly Type ConventionType;
        public readonly IInstallConvention ConventionInstance;
        public readonly Action<IVariableDictionary> LocalConvention;

        public ConventionThing(Type conventionType, Predicate<IVariableDictionary> condition = null)
        {
            this.ConventionType = conventionType;
            this.Condition = condition ?? Unconditional;
        }

        public ConventionThing(IInstallConvention conventionInstance, Predicate<IVariableDictionary> condition = null)
        {
            this.ConventionInstance = conventionInstance;
            this.Condition = condition ?? Unconditional;
        }


        public ConventionThing(Action<IVariableDictionary> localConvention, Predicate<IVariableDictionary> condition = null)
        {
            this.LocalConvention = localConvention;
            this.Condition = condition ?? Unconditional;
        }


        public Predicate<IVariableDictionary> Condition { get; private set; }

        private Func<IVariableDictionary, IEnumerable<object[]>> arguments;

        public Func<IVariableDictionary, IEnumerable<object[]>> Arguments
        {
            get
            {
                return arguments ?? NonArgumentative;
            }
            private set
            {
                if (arguments != null)
                {
                    throw new InvalidOperationException("Arguments have already been set");
                }
                arguments = value;
            }
        }



        public ConventionThing WithArguments(object[] args)
        {
            Arguments = (v) => new[] {args};
            return this;
        }

        public ConventionThing WithArguments(IEnumerable<object[]> args)
        {
            Arguments = (v) => args;
            return this;
        }

        public ConventionThing WithArguments(Func<IVariableDictionary, object[]> args)
        {
            Arguments = (v) => new[] {args(v)};
            return this;
        }

        public ConventionThing WithArguments(Func<IVariableDictionary, IEnumerable<object[]>> args)
        {
            Arguments = args;
            return this;
        }
    }

    public class ConventionSequence : IConventionSequence<IInstallConvention>
    {
        private readonly ConventionLocator conventionLocator;
        
        internal List<ConventionThing> Sequence = new List<ConventionThing>();


        internal ConventionSequence(ConventionLocator conventionLocator)
        {
            this.conventionLocator = conventionLocator;
        }

        public IConventionSequence<IInstallConvention> Clear()
        {

            Sequence.Clear();
            return this;
        }



        Type GetConvention(string name)
        {
            var conventionTYpe = conventionLocator.Locate(name);

            if (conventionTYpe == null)
            {
                throw new Exception($"Unabled to find convention with name '{name}'.");
            }
            return conventionTYpe;
        }




       



        public IConventionSequence<IInstallConvention> Run(IInstallConvention convention)
        {
            Sequence.Add(new ConventionThing(convention));
            return this;
        }

        public IConventionSequence<IInstallConvention> Run<TInstallConvention>(params object[] constructorArgs)
          where TInstallConvention : IInstallConvention
        {
            Sequence.Add(new ConventionThing(typeof(TInstallConvention)).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> Run(string conventionName, params object[] constructorArgs)
        {
            Sequence.Add(new ConventionThing(GetConvention(conventionName)).WithArguments(constructorArgs));
            return this;
        }
      
        public IConventionSequence<IInstallConvention> Run(string conventionName, Func<IVariableDictionary, object[]> constructorArgs)
        {
            Sequence.Add(new ConventionThing(GetConvention(conventionName)).WithArguments(constructorArgs));
            return this;
        }
        
        public IConventionSequence<IInstallConvention> Run<TInstallConvention>(Func<IVariableDictionary, object[]> constructorArgs) where TInstallConvention : IInstallConvention
        {
            Sequence.Add(new ConventionThing(typeof(TInstallConvention)).WithArguments(constructorArgs));
            return this;
        }



        public IConventionSequence<IInstallConvention> RunConditional<TInstallConvention>(Predicate<IVariableDictionary> condition, params object[] constructorArgs)
            where TInstallConvention : IInstallConvention
        {
            Sequence.Add(new ConventionThing(typeof(TInstallConvention), condition).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> RunConditional(Predicate<IVariableDictionary> condition, string conventionName, params object[] constructorArgs)
        {

            Sequence.Add(new ConventionThing(GetConvention(conventionName), condition).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> RunConditional(Predicate<IVariableDictionary> condition, IInstallConvention convention)
        {
            Sequence.Add(new ConventionThing(convention, condition));
            return this;
        }

        public IConventionSequence<IInstallConvention> RunConditional(Predicate<IVariableDictionary> condition, string conventionName, Func<IVariableDictionary, object[]> constructorArgs)
        {
            Sequence.Add(new ConventionThing(GetConvention(conventionName), condition).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> RunConditional<TInstallConvention>(Predicate<IVariableDictionary> condition, Func<IVariableDictionary, object[]> constructorArgs) where TInstallConvention : IInstallConvention
        {
            Sequence.Add(new ConventionThing(typeof(TInstallConvention), condition).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> RunForEach(Func<IVariableDictionary, IEnumerable<string[]>> constructorArgs, string conventionName)
        {
            Sequence.Add(new ConventionThing(GetConvention(conventionName)).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> RunForEach<TInstallConvention>(Func<IVariableDictionary, IEnumerable<string[]>> constructorArgs) where TInstallConvention : IInstallConvention
        {
            Sequence.Add(new ConventionThing(typeof(TInstallConvention)).WithArguments(constructorArgs));
            return this;
        }

        public IConventionSequence<IInstallConvention> Run(Action<IVariableDictionary> localConvention)
        {
            Sequence.Add(new ConventionThing(localConvention));
            return this;
        }

        public IConventionSequence<IInstallConvention> Run(Predicate<IVariableDictionary> condition, Action<IVariableDictionary> localConvention)
        {
            Sequence.Add(new ConventionThing(localConvention, condition));
            return this;
        }
    }
}