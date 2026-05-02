// Polyfill: C# 9 init-only properties and records emit references to this type,
// which does not exist in netstandard2.1 or net461. Declaring it here satisfies
// the compiler without any runtime cost.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
