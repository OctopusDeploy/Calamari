using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Integration.Nginx
{
    public class NginxMissingRootLocationException : Exception
    {
        public NginxMissingRootLocationException()
            : base("")
        { }

        public NginxMissingRootLocationException(string message)
            : base(message)
        { }
    }
}
