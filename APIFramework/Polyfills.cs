// ---------------------------------------------------------------------------
// Polyfills.cs
// Provides compiler-support types that exist in .NET 5+ but are absent from
// the netstandard2.1 BCL.  The C# compiler emits references to these types
// when you use C# 9+ features (init accessors, record types) — declaring them
// here satisfies the compiler without any runtime overhead.
// ---------------------------------------------------------------------------

// ReSharper disable All
#pragma warning disable CS0436   // type conflicts with imported type — intentional

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved for use by the C# compiler.  Required for init-only setters
    /// (C# 9 init accessors) and record types on targets below .NET 5.
    /// </summary>
    internal static class IsExternalInit { }
}
