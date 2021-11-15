using System;
using System.Linq;
using Conventional;
using Conventional.Conventions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Calamari.Tests.Fixtures.Conventions.ConventionSpecifications
{
    public class MustNotCreateNewInstancesOfConventionSpecification : ConventionSpecification
    {
        readonly string[] forbiddenTypeNames;

        public MustNotCreateNewInstancesOfConventionSpecification(Type[] forbiddenTypes, string failureMessage)
        {
            if (!forbiddenTypes.Any()) throw new ArgumentException("At least one forbidden type must be provided", nameof(forbiddenTypes));

            forbiddenTypeNames = forbiddenTypes.Select(t => t.FullName).Where(t => t != null).ToArray();
            FailureMessage = failureMessage;
        }

        protected override string FailureMessage { get; } = "Type creates instance(s) of forbidden type:";

        public override ConventionResult IsSatisfiedBy(Type type)
        {
            var instructions = DecompilationCache.InstructionsFor(type);

            var forbiddenNewObjInstructions = instructions
                .Where(i => i.OpCode == OpCodes.Newobj)
                .Where(i => i.Operand is MethodReference methodReference && forbiddenTypeNames.Contains(methodReference.DeclaringType.FullName))
                .ToArray();

            return !forbiddenNewObjInstructions.Any()
                ? ConventionResult.Satisfied(type.FullName)
                : ConventionResult.NotSatisfied(type.FullName,
                    FailureMessage + Environment.NewLine + string.Join(Environment.NewLine, forbiddenNewObjInstructions.Select(x => $" - {x}")));
        }
    }
}