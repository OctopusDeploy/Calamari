using System;

namespace Calamari.ArgoCD.Helm
{
    public class InvalidHelmImageReplaceAnnotationsException : Exception
    {
        /// <summary>
        /// In place to reject Argo Applications where the Annotations have been setup incorrectly. Examples may include:
        /// 1. Multiple inline values files, but the use of `OctopusImageReplacementPathsKey` without aliases setup
        /// 2. 
        /// </summary>
        public InvalidHelmImageReplaceAnnotationsException(string message) : base(message)
        {
        }
    }
}
