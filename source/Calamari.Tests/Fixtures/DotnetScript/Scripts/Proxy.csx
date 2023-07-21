using System;

Console.WriteLine("HTTP_PROXY:"+Environment.GetEnvironmentVariable("HTTP_PROXY"));
Console.WriteLine("HTTPS_PROXY:"+Environment.GetEnvironmentVariable("HTTPS_PROXY"));
Console.WriteLine("NO_PROXY:"+Environment.GetEnvironmentVariable("NO_PROXY"));

if (Environment.OSVersion.Platform == PlatformID.Win32NT)
{
    var testUri = new Uri("http://octopustesturl.com");
    var octopusProxyUri = System.Net.WebRequest.DefaultWebProxy.GetProxy(testUri);
    if (octopusProxyUri is not null && octopusProxyUri.Host != "octopustesturl.com")
    {
        Console.WriteLine("WebRequest.DefaultProxy:" + octopusProxyUri);
    }
    else
    {
        Console.WriteLine("WebRequest.DefaultProxy:None");
    }
}

var bypassUri = Environment.GetEnvironmentVariable("TEST_ONLY_PROXY_EXCEPTION_URI");
if (!string.IsNullOrEmpty(bypassUri))
{
    var bypass = new Uri(bypassUri);
    // Getting false from IsBypassed does not guarantee that the URI is proxied; 
    // we still need to call the GetProxy method to determine this.
    // https://learn.microsoft.com/en-us/dotnet/api/system.net.iwebproxy.isbypassed?view=net-6.0#remarks
    if (System.Net.WebRequest.DefaultWebProxy.IsBypassed(bypass) || 
        System.Net.WebRequest.DefaultWebProxy.GetProxy(bypass) == null)
    {
        Console.WriteLine("ProxyBypassed:" + bypassUri);
    }
}