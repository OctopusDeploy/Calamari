using System;

Console.WriteLine("HTTP_PROXY:"+Environment.GetEnvironmentVariable("HTTP_PROXY"));
Console.WriteLine("HTTPS_PROXY:"+Environment.GetEnvironmentVariable("HTTPS_PROXY"));
Console.WriteLine("NO_PROXY:"+Environment.GetEnvironmentVariable("NO_PROXY"));

if (Environment.OSVersion.Platform == PlatformID.Win32NT)
{
    var testUri = new Uri("http://octopustesturl.com");
    var proxyUri = System.Net.WebRequest.DefaultWebProxy.GetProxy(testUri);
    if (proxyUri.Host != "octopustesturl.com")
    {
        Console.WriteLine("WebRequest.DefaultProxy:" + proxyUri);
    }
    else
    {
        Console.WriteLine("WebRequest.DefaultProxy:None");
    }
}