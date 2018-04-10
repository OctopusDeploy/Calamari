using System;
using System.Collections.Generic;
using System.Text;

namespace Calamari.Deployment.Conventions
{
    public static class ConventionExtensions
    {
        public static ConditionalInstallationConvention<T> When<T>(this T convention, Func<RunningDeployment, bool> predicate) where T: IInstallConvention
        {
            return new ConditionalInstallationConvention<T>(predicate, convention);
        }
    }
}
