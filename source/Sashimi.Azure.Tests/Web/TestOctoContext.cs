using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using Newtonsoft.Json;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Sashimi.Azure.Tests.Web
{
    public class TestOctoContext : OctoContext
    {
        public TestOctoContext(TestOctoResponse response, IPrincipal? user = null) : base(new TestOctoRequest(), response, user)
        {
            TestResponse = response;
        }

        public TestOctoResponse TestResponse { get; }
    }

    public class TestOctoRequest : OctoRequest
    {
        public TestOctoRequest()
            : base("Http",
                   true,
                   "localhost",
                   "/",
                   "",
                   "HTTPS",
                   new MemoryStream(),
                   new Dictionary<string, IEnumerable<string>>(),
                   new Dictionary<string, IEnumerable<string>>(),
                   new Dictionary<string, IEnumerable<string>>(),
                   new Dictionary<string, string>())
        {
        }
    }

    public class TestOctoResponse : OctoResponse
    {
        public string? ResponseAsJson { get; private set; }

        public T GetResponse<T>()
        {
            if (ResponseAsJson == null)
                throw new InvalidOperationException("ResponseAsJson has not been set, call `.AsOctopusJson()`");
            return JsonConvert.DeserializeObject<T>(ResponseAsJson);
        }

        public override OctoResponse AsOctopusJson(object model)
        {
            ResponseAsJson = JsonConvert.SerializeObject(model);

            return this;
        }
    }
}