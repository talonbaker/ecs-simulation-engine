using System.Runtime.CompilerServices;

// Expose the ECSCli internal surface (such as the AI command Run() helpers)
// to the ECSCli.Tests project so unit/integration tests can drive them
// directly without going through Process.Start.
[assembly: InternalsVisibleTo("ECSCli.Tests")]
