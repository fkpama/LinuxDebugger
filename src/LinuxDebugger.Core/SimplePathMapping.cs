using System;

namespace LinuxDebugger;

public readonly struct SimplePathMapping : IEquatable<SimplePathMapping>
{
    public string Source { get; }
    public string Target { get; }
    public SimplePathMapping(string target)
        : this(target, target) { }
    public SimplePathMapping(string source, string target)
    {
        this.Source = source;
        this.Target = target;
    }

    public static implicit operator SimplePathMapping(string target)
        => new(target);

    public static bool operator ==(SimplePathMapping left, SimplePathMapping right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SimplePathMapping left, SimplePathMapping right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        return obj is SimplePathMapping mapping && this.Equals(mapping);
    }

    public bool Equals(SimplePathMapping other)
    {
        return this.Source == other.Source &&
               this.Target == other.Target;
    }

    public override int GetHashCode()
    {
        return 123475 +
            this.Source?.GetHashCode() ?? 0
            + this.Target?.GetHashCode() ?? 0;
    }
}
