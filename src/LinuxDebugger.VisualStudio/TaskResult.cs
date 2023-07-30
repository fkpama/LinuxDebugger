namespace LinuxDebugger.VisualStudio
{
    public static class TaskResult
    {
        private static class NullTaskResult<T> where T : class
        {
            public static readonly Task<T?> Instance = Task.FromResult<T?>(null);
        }

        private static class EmptyEnumerableTaskResult<T>
        {
            public static readonly Task<IEnumerable<T>> Instance = Task.FromResult(Enumerable.Empty<T>());
        }

        public static Task<bool> False => TplExtensions.FalseTask;

        public static Task<bool> True => TplExtensions.TrueTask;

        public static Task<string> FalseString => Task.FromResult(bool.FalseString);

        public static Task<string> TrueString => Task.FromResult(bool.TrueString);

        public static Task<string> EmptyString => Task.FromResult("");

        public static Task<T?> Null<T>() where T : class
        {
            return NullTaskResult<T>.Instance;
        }

        public static Task<IEnumerable<T>> EmptyEnumerable<T>()
        {
            return EmptyEnumerableTaskResult<T>.Instance;
        }
    }

}
