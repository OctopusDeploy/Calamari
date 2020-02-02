namespace Calamari.Tests.Fixtures
{
    public class RequiresMonoVersion480OrAboveForTls12Attribute : RequiresMinimumMonoVersionAttribute
    {
        /// <summary>
        /// TLSv1.2 was only provided from Mono 4.8.0. Running 
        /// </summary>
        public RequiresMonoVersion480OrAboveForTls12Attribute()
            : base(4, 8, 0)
        {

        }
    }
}