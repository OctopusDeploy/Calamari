using System;
using Calamari.Extensibility.Features;

namespace Calamari.Features
{
    public class FeatureExtension
    {
        public Type Feature { get; set; }
        public FeatureAttribute Details { get; set; }
    }
}