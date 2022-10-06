using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Integration.Nginx
{
    public class NginxConfigurationValidationFailedException : Exception
    {
        public NginxConfigurationValidationFailedException()
            : base ("")
        { }

        public NginxConfigurationValidationFailedException(string message)
            : base(message)
        { }
    }
}
