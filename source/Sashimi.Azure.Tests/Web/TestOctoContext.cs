using Newtonsoft.Json;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Sashimi.Azure.Tests.Web
{
    public class TestOctoContext : OctoContext
    {
        public TestOctoContext()
        {
            Response = TestResponse = new TestOctoResponse();
        }

        public override OctoResponse Response { get; }

        public TestOctoResponse TestResponse { get; }
    }

    public class TestOctoResponse : OctoResponse
    {
        public string ResponseAsJson { get; private set; }

        public T GetResponse<T>()
        {
            return JsonConvert.DeserializeObject<T>(ResponseAsJson);
        } 

        public override OctoResponse AsOctopusJson(object model)
        {
            ResponseAsJson = JsonConvert.SerializeObject(model);

            return this;
        }
    }
}