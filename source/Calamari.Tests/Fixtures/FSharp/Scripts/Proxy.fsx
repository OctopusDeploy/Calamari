"HTTP_PROXY" |> System.Environment.GetEnvironmentVariable |> printfn "HTTP_PROXY:%s"
"HTTPS_PROXY" |> System.Environment.GetEnvironmentVariable |> printfn "HTTPS_PROXY:%s"
"NO_PROXY" |> System.Environment.GetEnvironmentVariable |> printfn "NO_PROXY:%s"

let proxyUrl = new System.Uri("http://octopustesturl.com") |> System.Net.WebRequest.DefaultWebProxy.GetProxy
let actualProxyUrl = if proxyUrl.Host = "octopustesturl.com" then "None" else proxyUrl.ToString()
actualProxyUrl |> printfn "WebRequest.DefaultProxy:%s" 