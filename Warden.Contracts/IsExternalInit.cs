// Polyfill required for C# 9 init-only property setters on netstandard2.1.
// The type ships in-box from .NET 5 / netcoreapp3.1+; on netstandard2.1
// the compiler still emits a reference to it, so we declare it here.
// The conditional compile-guard prevents a "duplicate type" error if the
// project is ever multi-targeted against a TFM that already has it.
#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
