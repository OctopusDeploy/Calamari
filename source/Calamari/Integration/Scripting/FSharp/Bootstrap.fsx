module Octopus

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Net
open System.Security.Cryptography

let private encode (value:string) = System.Text.Encoding.UTF8.GetBytes(value) |> Convert.ToBase64String
let private decode (value:string) = Convert.FromBase64String(value) |> System.Text.Encoding.UTF8.GetString

let private writeServiceMessage name content = 
    printfn "##octopus[%s %s]" name content

let tryFindVariable name =
    match name |> encode with
{{VariableDeclarations}}

let findVariable name =
    match name |> tryFindVariable with
    | Some x -> x
    | None -> raise (System.Collections.Generic.KeyNotFoundException(name + "variable has not been found."))     

let decryptString encrypted iv =
    let key =  fsi.CommandLineArgs.[fsi.CommandLineArgs.Length - 1]
    use algorithm = new AesCryptoServiceProvider(Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7, KeySize = 128, BlockSize = 128, Key = Convert.FromBase64String(key), IV =  Convert.FromBase64String(iv))
    use dec = algorithm.CreateDecryptor()
    use ms = new MemoryStream(Convert.FromBase64String(encrypted))
    use cs = new CryptoStream(ms, dec, CryptoStreamMode.Read)
    use sr = new StreamReader(cs, Encoding.UTF8)
    sr.ReadToEnd();

let initializeProxy () =
    let (|Empty|NonEmpty|) value = if String.IsNullOrWhiteSpace value then Empty else NonEmpty value
    let proxyUsername = Environment.GetEnvironmentVariable "TentacleProxyUsername"
    let proxyPassword = Environment.GetEnvironmentVariable "TentacleProxyPassword"
    let credentials = 
        match proxyUsername with
        | Empty -> CredentialCache.DefaultCredentials
        | NonEmpty u -> new NetworkCredential(u, proxyPassword) :> ICredentials
    WebRequest.DefaultWebProxy.Credentials <- credentials
        
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