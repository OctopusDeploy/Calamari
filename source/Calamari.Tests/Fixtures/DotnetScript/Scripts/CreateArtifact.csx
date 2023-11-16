
using System;
using System.IO;

System.IO.Directory.CreateDirectory("Temp");
System.IO.File.WriteAllBytes(Path.Combine("Temp","myFile.txt"), new byte[100]);

CreateArtifact(Path.Combine("Temp","myFile.txt"));

