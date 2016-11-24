namespace Calamari.Tests.Integration.Fixtures
{
    public class RequiresMonoVersion400OrAboveAttribute : RequiresMinimumMonoVersionAttribute
    {
        public RequiresMonoVersion400OrAboveAttribute() 
            : base(4, 0, 0)
        {
            
        }
    }
}