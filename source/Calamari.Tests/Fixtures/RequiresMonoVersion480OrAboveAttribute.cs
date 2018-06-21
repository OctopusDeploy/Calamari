namespace Calamari.Tests.Fixtures
{
    public class RequiresMonoVersion480OrAboveAttribute : RequiresMinimumMonoVersionAttribute
    {
        /// <summary>
        /// TLSv1.2 was only provided from Mono 4.8.0. Running 
        /// </summary>
        public RequiresMonoVersion480OrAboveAttribute()
            : base(4, 8, 0)
        {

        }
    }
}