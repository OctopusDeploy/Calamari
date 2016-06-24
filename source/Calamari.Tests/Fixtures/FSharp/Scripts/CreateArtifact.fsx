open System
open System.IO

let bytes : byte array = Array.zeroCreate 100
System.IO.Directory.CreateDirectory("Temp")
System.IO.File.WriteAllBytes(Path.Combine("Temp","myFile.txt"), bytes)
let path = Path.Combine("Temp","myFile.txt")

Octopus.createArtifact path None
