using System;

namespace Calamari.Tests.AWS
{
    public static class RegionRandomiser
    {
        static Random random = new Random();

        static string[] regions = new string[]
        {
            "ap-northeast-1",
            "ap-northeast-2",
            "ap-northeast-3",
            "ap-south-1",
            "ap-southeast-1",
            "ap-southeast-2",
            "ca-central-1",
            "eu-central-1",
            "eu-north-1",
            "eu-west-1",
            "eu-west-2",
            "eu-west-3",
            "sa-east-1",
            "us-east-1",
            "us-east-2",
            "us-west-1",
            "us-west-2"
        };

        public static string GetARegion()
        {
            return regions[random.Next(0, regions.Length - 1)];
        }
    }
}