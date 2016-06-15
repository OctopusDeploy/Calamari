module Bootstrap

open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Net
open System.Security.Cryptography

type OctopusParametersDictionary(values : IDictionary<string, string>, key) = 
    inherit System.Collections.Generic.Dictionary<string,string>(values, System.StringComparer.OrdinalIgnoreCase)
    member this.Key =  Convert.FromBase64String(key)
    member this.DecryptString(encrypted, iv) =
        use algorithm = new AesCryptoServiceProvider(Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7, KeySize = 128, BlockSize = 128, Key = this.Key, IV =  Convert.FromBase64String(iv))
        use dec = algorithm.CreateDecryptor()
        use ms = new MemoryStream(Convert.FromBase64String(encrypted))
        use cs = new CryptoStream(ms, dec, CryptoStreamMode.Read)
        use sr = new StreamReader(cs, Encoding.UTF8)
        sr.ReadToEnd();

type Octopus(password) =
    do  
        let proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername")
        let proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword")
        WebRequest.DefaultWebProxy.Credentials = if System.String.IsNullOrWhiteSpace(proxyUsername) then CredentialCache.DefaultCredentials else (new NetworkCredential(proxyUsername, proxyPassword) :> ICredentials)
        |> ignore
    member this.Parameters = new OctopusParametersDictionary(password)
    member this.EncodeServiceMessageValue (value:string) = System.Text.Encoding.UTF8.GetBytes(value) |> Convert.ToBase64String
    member this.SetVariable name value = 
        let encodedName = this.EncodeServiceMessageValue name
        let encodedValue = this.EncodeServiceMessageValue value
        //Not sure why, but when we call [key] = value for a key that does not exist then we get KeyNotFoundException
        if this.Parameters.ContainsKey encodedName then this.Parameters.[encodedName] = encodedValue |> ignore else this.Parameters.Add(encodedName, encodedValue)
        printfn "##octopus[setVariable name='%s' value='%s']" encodedName encodedValue
    member this.CreateArtifact path fileName =
        let finalFileName = match fileName with
                            | Some value -> value |> this.EncodeServiceMessageValue
                            | None -> System.IO.Path.GetFileName(path) |> this.EncodeServiceMessageValue
        let length = (if System.IO.File.Exists(path) then (new System.IO.FileInfo(path)).Length.ToString() else "0") |> this.EncodeServiceMessageValue
        let finalPath = System.IO.Path.GetFullPath(path) |> this.EncodeServiceMessageValue
        printfn "##octopus[createArtifact path='%s' name='%s' length='%s']" finalPath finalFileName length

let Octopus = new Octopus(new Dictionary<string, string>(dict[ {{VariableDeclarations}} ]), fsi.CommandLineArgs.[fsi.CommandLineArgs.Length - 1])