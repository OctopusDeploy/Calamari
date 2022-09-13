using System;

Console.WriteLine("HTTP_PROXY:"+Environment.GetEnvironmentVariable("HTTP_PROXY"));
Console.WriteLine("HTTPS_PROXY:"+Environment.GetEnvironmentVariable("HTTPS_PROXY"));
Console.WriteLine("NO_PROXY:"+Environment.GetEnvironmentVariable("NO_PROXY"));

if (Environment.OSVersion.Platform == PlatformID.Win32NT)
{
    var testUri = new Uri("http://octopustesturl.com");
    var octopusProxyUri = System.Net.WebRequest.DefaultWebProxy.GetProxy(testUri);
    if (octopusProxyUri.Host != "octopustesturl.com")
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
    if(System.Net.WebRequest.DefaultWebProxy.IsBypassed(new Uri(bypassUri)))
    {
        Console.WriteLine("ProxyBypassed:" + bypassUri);
    }
}