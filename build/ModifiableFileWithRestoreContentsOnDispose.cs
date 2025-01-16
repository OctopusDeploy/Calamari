// ReSharper disable RedundantUsingDirective
using System;
using System.IO;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

public class ModifiableFileWithRestoreContentsOnDispose : IDisposable
{
    public readonly AbsolutePath FilePath;
    readonly string OriginalFileText;
    string FileText;
    bool disposed = false;
        
    public ModifiableFileWithRestoreContentsOnDispose(AbsolutePath filePath)
    {
        FilePath = filePath;
        OriginalFileText = File.ReadAllText(filePath);
        FileText = File.ReadAllText(filePath);
    }
        
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        RestoreContents();
    }

    public void RestoreContents()
    {
        Log.Information($"Restoring file {FilePath}");
        File.WriteAllText(FilePath, OriginalFileText);
    }
        
    public void ReplaceRegexInFiles(string matchingPattern, string replacement)
    {
        FileText = Regex.Replace(FileText, matchingPattern, replacement);
        File.WriteAllText(FilePath, FileText);
    }

    public void ReplaceTextInFile(string textToReplace, string replacementValue)
    {
        FileText = FileText.Replace(textToReplace, replacementValue);
        File.WriteAllText(FilePath, FileText);
    }
}