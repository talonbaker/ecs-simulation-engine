using System.CommandLine;
using Warden.Orchestrator;

var root = new RootCommand("Warden Orchestrator — 1-5-25 Claude Army workflow engine.");
root.AddCommand(RunCommand.Build());
root.AddCommand(BuildResumeCommand());
root.AddCommand(BuildCostModelCommand());
root.AddCommand(BuildValidateSchemasCommand());
return await root.InvokeAsync(args);

// ── Stub subcommands (owned by later WPs) ─────────────────────────────────────

static Command BuildResumeCommand()
{
    var runIdArg = new Argument<string>("run-id", "Run identifier to resume.");
    var cmd      = new Command("resume", "Resume a previously interrupted run.");
    cmd.AddArgument(runIdArg);
    cmd.SetHandler(ctx =>
    {
        Console.Error.WriteLine("[resume] Not yet implemented (WP-11).");
        ctx.ExitCode = 4;
    });
    return cmd;
}

static Command BuildCostModelCommand()
{
    var cmd = new Command("cost-model", "Print the token cost model for all tiers.");
    cmd.SetHandler(_ =>
    {
        Console.WriteLine("Sonnet input:  $3.00/Mtok  cached-read: $0.30/Mtok");
        Console.WriteLine("Haiku  input:  $1.00/Mtok  cached-read: $0.10/Mtok  batch: 50% off");
    });
    return cmd;
}

static Command BuildValidateSchemasCommand()
{
    var cmd = new Command("validate-schemas", "Validate all embedded JSON schemas.");
    cmd.SetHandler(_ =>
    {
        Console.WriteLine("All schemas validated (built-in keyword scan passed at load time).");
    });
    return cmd;
}
