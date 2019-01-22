using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Integration.Nginx
{
    public class NginxReloadConfigurationFailedException : Exception
    {
        public NginxReloadConfigurationFailedException()
            : base("")
        { }

        public NginxReloadConfigurationFailedException(string message)
            : base(message)
        { }
    }
}
