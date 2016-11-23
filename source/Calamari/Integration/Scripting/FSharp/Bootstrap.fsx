module Octopus

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Net
open System.Security.Cryptography

let private encode (value:string) = System.Text.Encoding.UTF8.GetBytes(value) |> Convert.ToBase64String
let private decode (value:string) = Convert.FromBase64String(value) |> System.Text.Encoding.UTF8.GetString

let private writeServiceMessage name content =  printfn "##octopus[%s %s]" name content

let private getEnvironmentVariable name =
    let value = Environment.GetEnvironmentVariable name
    if String.IsNullOrWhiteSpace value then None else Some value

let private getCustomProxy proxyHost = 
    let proxyPort = match "TentacleProxyPort" |> getEnvironmentVariable with
                    | Some x -> Int32.Parse(x)
                    | None -> 0
    (new WebProxy((new UriBuilder("http", proxyHost, proxyPort)).Uri)) :> IWebProxy

let private getCustomCredentials proxyUserName = 
    let proxyPassword = match "TentacleProxyPassword" |> getEnvironmentVariable with
                        | Some x -> x
                        | None -> raise (new System.Exception("Password for proxy is required"))

    new NetworkCredential(proxyUserName, proxyPassword)

let private decryptString encrypted iv =
    let key =  fsi.CommandLineArgs.[fsi.CommandLineArgs.Length - 1]
    use algorithm = new AesCryptoServiceProvider(Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7, KeySize = 128, BlockSize = 128, Key = Convert.FromBase64String(key), IV =  Convert.FromBase64String(iv))
    use decryptor = algorithm.CreateDecryptor()
    use memoryStream = new MemoryStream(Convert.FromBase64String(encrypted))
    use cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read)
    use streamReader = new StreamReader(cryptoStream, Encoding.UTF8)
    streamReader.ReadToEnd();

let private logEnvironmentInformation () = ""

let tryFindVariable name =
    match name |> encode with
(*{{VariableDeclarations}}*)

let findVariable name =
    match name |> tryFindVariable with
    | Some x -> x
    | None -> raise (System.Collections.Generic.KeyNotFoundException(name + " variable has not been found."))   

let findVariableOrDefault defaultValue name =
    match name |> tryFindVariable with
    | Some x -> x
    | None -> defaultValue

let initializeProxy () =
    let proxyHost = "TentacleProxyHost" |> getEnvironmentVariable 
    let proxy = match proxyHost with
                | Some x -> getCustomProxy x
                | None -> WebRequest.GetSystemWebProxy()

    let proxyUserName  = getEnvironmentVariable "TentacleProxyUsername" 
    let credentials = match proxyUserName with
                        | Some x -> getCustomCredentials x
                        | None -> match proxyHost with
                                    | Some x -> new NetworkCredential() 
                                    | None -> CredentialCache.DefaultNetworkCredentials
    proxy.Credentials <- (credentials :> ICredentials)
    WebRequest.DefaultWebProxy <- proxy
        
let setVariable name value = 
    let encodedName = encode name
    let encodedValue = encode value
    let content = sprintf "name='%s' value='%s'" encodedName encodedValue
    writeServiceMessage "setVariable" content

let createArtifact path fileName =
    let encodedFileName = match fileName with
                            | Some value -> value |> encode
                            | None -> System.IO.Path.GetFileName(path) |> encode

    let encodedPath = System.IO.Path.GetFullPath(path) |> encode

    let encodedLength = (if System.IO.File.Exists(path) then (new System.IO.FileInfo(path)).Length else 0L) |> string |> encode

    let content = sprintf "path='%s' name='%s' length='%s'"  encodedPath encodedFileName encodedLength
    writeServiceMessage "createArtifact" content

logEnvironmentInformation()
