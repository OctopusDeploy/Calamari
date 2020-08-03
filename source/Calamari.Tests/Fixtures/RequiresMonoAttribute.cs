namespace Calamari.Tests.Fixtures
{
    public class RequiresMonoAttribute : RequiresMinimumMonoVersionAttribute
    {
        public RequiresMonoAttribute() 
            : base(1, 0, 0)
        {
            
        }
    }
}