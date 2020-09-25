using System;
using System.Collections.Generic;
using System.Text;

namespace Spike
{
    public class PublishProfileCreds
    {
        public string UserName { get; }
        public string Pwd { get; }

        public PublishProfileCreds(string userName, string pwd)
        {
            UserName = userName;
            Pwd = pwd;
        }
    }
}
