namespace Calamari.IntegrationTests.Helpers.FilterAttributes
{
    public class RequiresMonoVersion423OrAboveAttribute : RequiresMinimumMonoVersionAttribute
    {
        public RequiresMonoVersion423OrAboveAttribute()
            : base(4, 2, 3)
        {

        }
    }
}