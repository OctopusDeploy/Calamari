namespace Calamari.IntegrationTests.Helpers.FilterAttributes
{
    public class RequiresMonoVersion400OrAboveAttribute : RequiresMinimumMonoVersionAttribute
    {
        public RequiresMonoVersion400OrAboveAttribute() 
            : base(4, 0, 0)
        {
            
        }
    }
}