module Octopus

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open System.Net
open System.Security.Cryptography
open System.Security.Principal

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
    use algorithm = new AesCryptoServiceProvider(Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7, KeySize = 256, BlockSize = 128, Key = Convert.FromBase64String(key), IV =  Convert.FromBase64String(iv))
    use decryptor = algorithm.CreateDecryptor()
    use memoryStream = new MemoryStream(Convert.FromBase64String(encrypted))
    use cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read)
    use streamReader = new StreamReader(cryptoStream, Encoding.UTF8)
    streamReader.ReadToEnd();

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
    let useDefaultProxy = match "TentacleUseDefaultProxy" |> getEnvironmentVariable with
                            | Some x -> Boolean.Parse(x)
                            | None -> true

    let proxyHost = "TentacleProxyHost" |> getEnvironmentVariable
    let useCustomProxy = match proxyHost with
                            | Some x -> true
                            | None -> false

    let proxy =        
        match proxyHost with
            | Some x -> getCustomProxy x
            | None -> if useDefaultProxy then WebRequest.GetSystemWebProxy() else new WebProxy() :> IWebProxy
    
    let proxyUserName  = getEnvironmentVariable "TentacleProxyUsername" 
    let credentials = match proxyUserName with
                        | Some x -> getCustomCredentials x
                        | None -> match proxyHost with
                                    | Some x -> new NetworkCredential() 
                                    | None -> CredentialCache.DefaultNetworkCredentials

    if (useCustomProxy || useDefaultProxy)
        then proxy.Credentials <- (credentials :> ICredentials)
        
    WebRequest.DefaultWebProxy <- proxy

let failStep message = 
    match message with
        | null -> Environment.Exit -1
        | m -> 
            let content = sprintf "message='%s'" (encode m)
            writeServiceMessage "resultMessage" content
            Environment.Exit -1
    

let setVariable name value = 
    let encodedName = encode name
    let encodedValue = encode value
    let content = sprintf "name='%s' value='%s'" encodedName encodedValue
    writeServiceMessage "setVariable" content
    
let setSensitiveVariable name value =
    let encodedName = encode name
    let encodedValue = encode value
    let content = sprintf "name='%s' value='%s' sensitive='%s'" encodedName encodedValue (encode "True")
    writeServiceMessage "setVariable" content

let createArtifact (path: String) fileName =
    let plainFileName = match fileName with
                            | Some value -> value
                            | None -> System.IO.Path.GetFileName(path)
    let encodedFileName = plainFileName |> encode

    let path = System.IO.Path.GetFullPath(path)
    let encodedPath = path |> encode

    let encodedLength = (if System.IO.File.Exists(path) then (new System.IO.FileInfo(path)).Length else 0L) |> string |> encode

    let content = sprintf "path='%s' name='%s' length='%s'"  encodedPath encodedFileName encodedLength
    printfn "##octopus[stdout-verbose]"
    printfn "Artifact %s will be collected from %s after this step completes" plainFileName path
    printfn "##octopus[stdout-default]"
    writeServiceMessage "createArtifact" content

let updateProgress (percentage: int) message =
    let encodedMessage = message |> encode
    let encodedPercentage = percentage.ToString() |> encode
    let content = sprintf "percentage='%s' message='%s'" encodedPercentage encodedMessage
    writeServiceMessage "progress" content
    
let writeVerbose message = 
    printfn "##octopus[stdout-verbose]"
    printfn message
    printfn "##octopus[stdout-default]"

let writeHighlight message = 
    printfn "##octopus[stdout-highlight]"
    printfn message
    printfn "##octopus[stdout-default]"

let writeWait message = 
    printfn "##octopus[stdout-wait]"
    printfn message
    printfn "##octopus[stdout-default]"

let writeWarning message = 
    printfn "##octopus[stdout-warning]"
    printfn message
    printfn "##octopus[stdout-default]"

let private safelyLogEnvironmentVars () =
    try
        printfn "  OperatingSystem: %s" (Environment.OSVersion.ToString())
        printfn "  OsBitVersion: %s" (if Environment.Is64BitOperatingSystem then "x64" else "x86")
        printfn "  Is64BitProcess: %s" (Environment.Is64BitProcess.ToString())
        printfn "  CurrentUser: %s" (WindowsIdentity.GetCurrent().Name)
        printfn "  MachineName: %s" (Environment.MachineName)
        printfn "  ProcessorCount: %s" (Environment.ProcessorCount.ToString())
    with
    | _ -> ()

let private safelyLogPathVars () =
    try
        printfn "  CurrentDirectory: %s" (Directory.GetCurrentDirectory())
        printfn "  TempDirectory: %s" (Path.GetTempPath())
    with
    | _ -> ()

let private safelyLogProcessVars () =
    try
        let proc = Process.GetCurrentProcess()
        printfn "  HostProcess: %s (%d)" proc.ProcessName proc.Id
    with
    | _ -> ()

let private logEnvironmentInformation () =
    try
        let suppressEnvironmentLogging = findVariableOrDefault "False" "Octopus.Action.Script.SuppressEnvironmentLogging"
        if suppressEnvironmentLogging = "True" then
            () // bail out
        else
            printfn "##octopus[stdout-verbose]"
            printfn "FSharp Environment Information:"
            safelyLogEnvironmentVars()
            safelyLogPathVars()
            safelyLogProcessVars()
            printfn "##octopus[stdout-default]"
    with
    | _ -> printfn "##octopus[stdout-default]"

logEnvironmentInformation()
