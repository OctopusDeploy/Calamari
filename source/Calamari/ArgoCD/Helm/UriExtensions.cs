using System;

namespace Calamari.ArgoCD.Helm
{
    public static class UriExtensionMethods
    {
        public static bool IsNullOrEmpty(this Uri uri) => string.IsNullOrWhiteSpace(uri?.OriginalString);

        public static Uri AddPath(this Uri uri, params string[] paths)
        {
            var abs = uri.AbsoluteUri;

            var newUri = new Uri(abs);
            foreach (var segment in paths)
            {
                newUri = new Uri($"{uri.AbsoluteUri.TrimEnd('/')}/{segment.TrimStart('/')}");
            }

            return newUri;
        }
    }
}
