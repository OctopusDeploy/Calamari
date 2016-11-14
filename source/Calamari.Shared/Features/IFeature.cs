using System;
using System.Collections;
using System.Collections.Generic;
using Calamari.Shared.Convention;

namespace Calamari.Shared.Features
{
    public interface IFeature
    {
        void ConfigureInstallSequence(IVariableDictionary variables, IConventionSequence<IInstallConvention> sequence);
        void Rolback(IVariableDictionary variables);
    }

//
//    public interface IRollbackFeature : IFeature
//    {
//        void Rollback(IFeatureExecutionContext context);
//    }


//    public interface IFeatureExecutionContext
//    {
//        ICalamariFileSystem FileSystem { get; set; }
//        IFeatureLocator FeatureLocator { get; set; }
//        IConventionSequence<IInstallConvention> ConventionSequence  {get; set;}
//    }





    public interface IConventionSequence<TConvention> where TConvention : IConvention
    {
        IConventionSequence<TConvention> Clear();

        IConventionSequence<TConvention> Run(Action<IVariableDictionary> localConvention);
        IConventionSequence<TConvention> Run(TConvention convention);
        IConventionSequence<TConvention> Run(string conventionName, params object[] constructorArgs);
        IConventionSequence<TConvention> Run<VConvention>(params object[] constructorArgs) where VConvention : TConvention;
        IConventionSequence<TConvention> Run(string conventionName, Func<IVariableDictionary, object[]> constructorArgs);
        IConventionSequence<TConvention> Run<VConvention>(Func<IVariableDictionary, object[]> constructorArgs) where VConvention : TConvention;


        IConventionSequence<TConvention> Run(Predicate<IVariableDictionary> condition, Action<IVariableDictionary> localConvention);
        IConventionSequence<TConvention> RunConditional(Predicate<IVariableDictionary> condition, TConvention convention);
        IConventionSequence<TConvention> RunConditional(Predicate<IVariableDictionary> condition, string conventionName, params object[] constructorArgs);
        IConventionSequence<TConvention> RunConditional<VConvention>(Predicate<IVariableDictionary> condition, params object[] constructorArgs) where VConvention : TConvention;
        IConventionSequence<TConvention> RunConditional(Predicate<IVariableDictionary> condition, string conventionName, Func<IVariableDictionary, object[]> constructorArgs);
        IConventionSequence<TConvention> RunConditional<VConvention>(Predicate<IVariableDictionary> condition, Func<IVariableDictionary, object[]> constructorArgs) where VConvention : TConvention;

        //IConventionSequence<TConvention> AddForEach(Func<IVariableDictionary, IEnumerable<string[]>> constructorArgs, TConvention convention);
        IConventionSequence<TConvention> RunForEach(Func<IVariableDictionary, IEnumerable<string[]>> constructorArgs, string conventionName);
        IConventionSequence<TConvention> RunForEach<VConvention>(Func<IVariableDictionary, IEnumerable<string[]>> constructorArgs) where VConvention : TConvention;
    }

}

