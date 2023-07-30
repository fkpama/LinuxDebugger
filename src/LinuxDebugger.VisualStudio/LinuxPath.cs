using liblinux.IO;

namespace LinuxDebugger.VisualStudio
{
    public static class LinuxPath
    {
        public static string ConvertWindowsPathToUnixPath(string pathUnixDir)
            => PathUtils.ConvertWindowsPathToUnixPath(pathUnixDir);
        public static string Normalize(string pathUnixDir)
            => PathUtils.NormalizePathSlow(pathUnixDir);
        public static string MakeUnixPath(string pathUnixDir, string relativeWindowsPath)
            => PathUtils.MakeUnixPath(pathUnixDir, relativeWindowsPath);

        public static string Combine(string path1, string path2)
            => PathUtils.CombineUnixPaths(path1, path2);

        public static bool IsUnixLike(string path)
            => PathUtils.IsUnixLikePath(path);

        public static string Combine(string path1, string path2, params string[] others)
            => PathUtils.CombineUnixPaths(path1, path2, others);
        public static string GetDirectoryName(string pathUnixDir)
            => PathUtils.GetUnixDirectoryName(pathUnixDir);

        public static string GetFilename(string pathUnixDir)
            => PathUtils.GetPathElements(pathUnixDir).Last();

        public static string EscapeForUnixShell(string path, params char[] additionalUnescapedChars)
            => PathUtils.EscapeStringForUnixShell(path, additionalUnescapedChars);
        public static bool IsRooted(string exePath) => PathUtils.IsAbsolutePath(exePath);

        public static string[] ParseCommandLine(string commandLine)
        {
            var arguments = new List<string>();
            var isInQuotes = false;
            var currentArgument = "";

            foreach (var c in commandLine)
            {
                if (c == '\"')
                {
                    isInQuotes = !isInQuotes;
                    currentArgument += c;
                }
                else if (c == ' ' && !isInQuotes)
                {
                    if (!string.IsNullOrWhiteSpace(currentArgument))
                    {
                        arguments.Add(currentArgument);
                        currentArgument = "";
                    }
                }
                else if (c == '\\')
                {
                    if (isInQuotes && commandLine.Length > 1)
                    {
                        var nextIndex = commandLine.IndexOf('\\', commandLine.IndexOf(c) + 1);

                        if (nextIndex != -1)
                        {
                            currentArgument += commandLine.Substring(commandLine.IndexOf(c) + 1, nextIndex - commandLine.IndexOf(c));
                            continue;
                        }
                    }

                    currentArgument += c;
                }
                else
                {
                    currentArgument += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentArgument))
            {
                arguments.Add(currentArgument);
            }

            return arguments.ToArray();
        }
    }
}
