using System.Collections.Generic;
using System.IO;
using System.Linq;
using APIFramework.Systems.Dialog;
using Xunit;

namespace APIFramework.Tests.Data;

/// <summary>
/// AT-11: The maskSlip fragments in corpus-starter.json are well-formed:
///   — at least 10 fragments present
///   — all have noteworthiness >= 70
///   — at least 3 distinct registers represented
///   — no duplicate IDs within the maskSlip context
///   — no fragment has empty text
/// </summary>
public class CorpusMaskSlipFragmentValidationTests
{
    private const string CorpusRelativePath = "docs/c2-content/dialog/corpus-starter.json";

    private static IReadOnlyList<PhraseFragment> LoadMaskSlipFragments()
    {
        var path = DialogCorpusService.FindCorpusFile(CorpusRelativePath);
        Assert.True(path is not null && File.Exists(path),
            $"corpus-starter.json not found via FindCorpusFile — expected relative path: {CorpusRelativePath}");

        var service = DialogCorpusService.LoadFromFile(path!);
        return service.AllFragments
            .Where(f => f.Context == "maskSlip")
            .ToList();
    }

    [Fact]
    public void AT11_AtLeastTenMaskSlipFragments()
    {
        var fragments = LoadMaskSlipFragments();
        Assert.True(fragments.Count >= 10,
            $"Expected >= 10 maskSlip fragments in corpus-starter.json, found {fragments.Count}");
    }

    [Fact]
    public void AT11_AllFragments_NoteworthinessAtLeast70()
    {
        var fragments = LoadMaskSlipFragments();
        foreach (var f in fragments)
            Assert.True(f.Noteworthiness >= 70,
                $"Fragment '{f.Id}' has noteworthiness {f.Noteworthiness} (minimum required: 70)");
    }

    [Fact]
    public void AT11_AtLeastThreeRegisters()
    {
        var fragments = LoadMaskSlipFragments();
        var registers = fragments.Select(f => f.Register).Distinct().OrderBy(r => r).ToList();
        Assert.True(registers.Count >= 3,
            $"Expected >= 3 distinct registers in maskSlip fragments; found: {string.Join(", ", registers)}");
    }

    [Fact]
    public void AT11_NoDuplicateIds()
    {
        var fragments = LoadMaskSlipFragments();
        var ids       = fragments.Select(f => f.Id).ToList();
        var distinct  = ids.Distinct().ToList();
        Assert.Equal(distinct.Count, ids.Count);
    }

    [Fact]
    public void AT11_AllFragments_HaveNonEmptyText()
    {
        var fragments = LoadMaskSlipFragments();
        foreach (var f in fragments)
            Assert.False(string.IsNullOrWhiteSpace(f.Text),
                $"Fragment '{f.Id}' has empty or whitespace-only text");
    }
}
