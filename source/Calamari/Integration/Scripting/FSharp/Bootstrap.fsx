module Octopus

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Net
open System.Security.Cryptography

let private encodeServiceMessageValue (value:string) = System.Text.Encoding.UTF8.GetBytes(value) |> Convert.ToBase64String

let private writeServiceMessage name content = 
    printfn "##octopus[%s %s]" name content

let variables  = [{{VariableDeclarations}}] |> Map.ofSeq

let tryFindVariable variables name = Map.tryFind name variables

let findVariable variables name = Map.find name variables

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
    let encodedName = encodeServiceMessageValue name
    let encodedValue = encodeServiceMessageValue value
    let content = sprintf "name='%s' value='%s'" encodedName encodedValue
    writeServiceMessage "setVariable" content  

let createArtifact path fileName =
    let encodedFileName = match fileName with
                            | Some value -> value |> encodeServiceMessageValue
                            | None -> System.IO.Path.GetFileName(path) |> encodeServiceMessageValue

    let encodedPath = System.IO.Path.GetFullPath(path) |> encodeServiceMessageValue

    let encodedLength = (if System.IO.File.Exists(path) then (new System.IO.FileInfo(path)).Length else 0L) |> string |> encodeServiceMessageValue

    let content = sprintf "path='%s' name='%s' length='%s'"  encodedPath encodedFileName encodedLength
    writeServiceMessage "createArtifact" content  