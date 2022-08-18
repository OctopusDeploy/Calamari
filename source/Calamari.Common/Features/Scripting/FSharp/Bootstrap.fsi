module Octopus
val private encode : value:string -> string
val private decode : value:string -> string
val private writeServiceMessage : name:string -> content:string -> unit
val private getEnvironmentVariable : name:string -> string option
val private getCustomProxy : proxyHost:string -> System.Net.IWebProxy
val private getCustomCredentials : proxyUserName:string -> System.Net.NetworkCredential
val private decryptString : encrypted:string -> iv:string -> string
val private safelyLogEnvironmentVars : unit -> unit
val private safelyLogPathVars : unit -> unit
val private safelyLogProcessVars : unit -> unit
val private logEnvironmentInformation : unit -> unit
val tryFindVariable : name:string -> string option
val findVariable : name:string -> string
val findVariableOrDefault : defaultValue:string -> name:string -> string
val initializeProxy : unit -> unit
val setVariable : name:string -> value:string -> unit
val createArtifact : path:string -> fileName:string option -> unit