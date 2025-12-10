using Nuke.Common;
using Nuke.Common.IO;

namespace Calamari.Build;

public static class KnownPaths
{
    public static AbsolutePath RootDirectory => NukeBuild.RootDirectory;
    public static AbsolutePath OutputsDirectory => RootDirectory / "outputs";

}