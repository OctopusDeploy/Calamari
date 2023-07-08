namespace Calamari.Common.Plumbing.FileSystem.GlobExpressions
{
    public class Group
    {
        public Group(int startIndex, int length, string[] options)
        {
            StartIndex = startIndex;
            Length = length;
            Options = options;
        }

        public int StartIndex { get; }

        public int Length { get; }

        public string[] Options { get; }
    }

    public class CharGroup : Group
    {
        public CharGroup(
            int startIndex,
            int length,
            string[] options,
            bool isRange) : base(
            startIndex, length, options)
        {
            IsRange = isRange;
        }

        public bool IsRange { get; }
    }
}