using Nuke.Common;
using Nuke.Common.IO;

namespace Calamari.Build;

public static class KnownPaths
{
    public static AbsolutePath RootDirectory => NukeBuild.RootDirectory;
    
    public static AbsolutePath SourceDirectory => RootDirectory / "source";

    public static AbsolutePath BuildDirectory => RootDirectory / "builds";

    public static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    public static AbsolutePath PublishDirectory => RootDirectory / "publish";
    
    public static AbsolutePath OutputsDirectory => RootDirectory / "outputs";

}