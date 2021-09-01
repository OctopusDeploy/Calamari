using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;

namespace Sashimi.Server.Contracts.Validation
{
    public class UniqueValidator<TCollectionElement> : AbstractValidator<IEnumerable<TCollectionElement>>
    {
        public UniqueValidator()
        {
            // Appease Autofac
        }

        public UniqueValidator(Expression<Func<TCollectionElement, string>> selector, string singular, string plural, IEqualityComparer<string>? equalityComparer = null)
        {
            var propertyName = selector.GetMember() != null ? selector.GetMember().Name : "self";
            RuleFor(p => p)
                .Custom((items, context) =>
                        {
                            var grouped = items.GroupBy(selector.Compile(), x => x, equalityComparer).Where(g => g.Count() > 1).ToList();
                            if (!grouped.Any())
                                return;

                            var duplicates = "'" + string.Join("', '", grouped.Select(g => g.Key).Take(10)) + "'" + (grouped.Count > 10 ? "..." : "");

                            var failure = grouped.Count == 1
                                ? new ValidationFailure(propertyName, string.Format(singular, duplicates))
                                : new ValidationFailure(propertyName, string.Format(plural, duplicates));

                            context.AddFailure(failure);
                        });
        }
    }
}