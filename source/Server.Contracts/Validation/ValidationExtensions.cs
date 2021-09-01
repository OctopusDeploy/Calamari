using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using FluentValidation;
using Newtonsoft.Json;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using PropertiesDictionary = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace Sashimi.Server.Contracts.Validation
{
    public static class ValidationExtensions
    {
        public static IRuleBuilderOptions<T, IEnumerable<TCollectionElement>> MustBeDistinct<T, TCollectionElement>(this IRuleBuilder<T, IEnumerable<TCollectionElement>> ruleBuilder, Expression<Func<TCollectionElement, string>> selector, string singular, string plural)
        {
            return ruleBuilder.SetValidator(new UniqueValidator<TCollectionElement>(selector, singular, plural));
        }

        public static IRuleBuilderOptions<T, PropertiesDictionary> ValidateSerializedProperty<T, TNested>(
            this IRuleBuilder<T, PropertiesDictionary> ruleBuilder,
            string property,
            Action<AbstractValidator<TNested>> nestedRules,
            Func<string, TNested>? deserialize = null)
        {
            var validator = new InlineValidator<TNested>();
            validator.RuleFor(x => x);

            nestedRules(validator);
            return ruleBuilder.SetValidator(new SerializedPropertyValidator<TNested>(property, validator, deserialize ?? SerializedPropertyValidator<TNested>.GetDefaultDeserializer()));
        }

        public static IRuleBuilderOptions<T, PropertiesDictionary> ValidateSerializedProperty<T, TNested>(
            this IRuleBuilder<T, PropertiesDictionary> ruleBuilder,
            string property,
            IValidator<TNested> validator,
            Func<string, TNested>? deserialize = null)
        {
            return ruleBuilder.SetValidator(new SerializedPropertyValidator<TNested>(property, validator, deserialize ?? SerializedPropertyValidator<TNested>.GetDefaultDeserializer()));
        }

        public static IRuleBuilderOptions<T, TProperty> WhenActionTypeIs<T, TProperty>(
            this IRuleBuilderOptions<T, TProperty> rule,
            string actionType)
            where T : DeploymentActionValidationContext
        {
            return rule.When(a => a.ActionType == actionType);
        }

        class SerializedPropertyValidator<TNested> : AbstractValidator<PropertiesDictionary>
        {
            public SerializedPropertyValidator()
            {
            }

            public SerializedPropertyValidator(string property, IValidator<TNested> validator, Func<string, TNested> deserialize)
            {
                RuleFor(p => p)
                    .Custom((properties, context) =>
                            {
                                var hasProperty = properties.TryGetValue(property, out var value);

                                if (!hasProperty || string.IsNullOrEmpty(value)) return;

                                try
                                {
                                    var deserializedValue = deserialize(properties[property]);

                                    //We don't use an indexer for the property dictionary as partial matches on the client side and would also make
                                    //the syntax quite horrible so use a property chain instead. We also keep the existing property chain by cloning.
                                    var newContext = context.ParentContext.CloneForChildValidator(deserializedValue);
                                    newContext.PropertyChain.Add(property);

                                    var result = validator.Validate(newContext);

                                    foreach (var error in result.Errors)
                                        context.AddFailure(error);
                                }
                                catch (JsonSerializationException exception)
                                {
                                    context.AddFailure($"The nested object shape is invalid: ${exception.Message}");
                                }
                            });
            }

            public static Func<string, TNested> GetDefaultDeserializer()
            {
                return value =>
                       {
                           var serializerSettings = JsonSerialization.GetDefaultSerializerSettings();

                           return JsonConvert.DeserializeObject<TNested>(value, serializerSettings)!;
                       };
            }
        }
    }
}