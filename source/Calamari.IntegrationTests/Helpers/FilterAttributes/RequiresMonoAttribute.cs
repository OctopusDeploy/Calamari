namespace Calamari.IntegrationTests.Helpers.FilterAttributes
{
    public class RequiresMonoAttribute : RequiresMinimumMonoVersionAttribute
    {
        public RequiresMonoAttribute() 
            : base(1, 0, 0)
        {
            
        }
    }
}