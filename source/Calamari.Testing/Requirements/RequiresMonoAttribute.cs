namespace Calamari.Testing.Requirements
{
    public class RequiresMonoAttribute : RequiresMinimumMonoVersionAttribute
    {
        public RequiresMonoAttribute() 
            : base(1, 0, 0)
        {
            
        }
    }
}