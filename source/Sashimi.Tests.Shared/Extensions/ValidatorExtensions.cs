using System;
using System.Linq;
using FluentValidation;

namespace Sashimi.Tests.Shared.Extensions
{
    public static class ValidatorExtensions
    {
        public static string[] ValidateAndGetErrors(this IValidator validator, object model)
        {
            var errors = validator.Validate(model).Errors.Select(e => e.ErrorMessage).ToArray();
            foreach (var error in errors)
                Console.WriteLine(error);

            return errors;
        }
    }
}