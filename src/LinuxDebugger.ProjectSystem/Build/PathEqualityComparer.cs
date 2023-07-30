namespace LinuxDebugger.ProjectSystem.Build
{
    internal sealed class PathEqualityComparer : IEqualityComparer<string>
    {
        public static readonly PathEqualityComparer Instance = new();
        public bool Equals(string x, string y)
        {
            if (x is null && y is null) return true;
            else if (x is null || y is null) return false;
            return PathHelper.IsSamePath(x, y);
        }

        public int GetHashCode(string obj)
        {
            return string.IsNullOrWhiteSpace(obj)
                ? 0
                : Path.GetFullPath(obj).GetHashCode();
        }
    }
}
