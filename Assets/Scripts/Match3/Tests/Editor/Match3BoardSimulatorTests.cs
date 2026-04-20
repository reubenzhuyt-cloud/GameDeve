using NUnit.Framework;

/// <summary>
/// 与 <see cref="Match3BoardSimulator"/> 行为对齐；表现层 <see cref="Match3Manager"/> 仅通过 TrySwap 提交，逻辑须与此一致。
/// </summary>
public sealed class Match3BoardSimulatorTests
{
    [Test]
    public void RandomInit_HasNoMatch_And_HasAvailableSwap()
    {
        var sim = new Match3BoardSimulator();
        sim.RandomInitNoMatches();
        Assert.That(sim.HasAnyMatch(), Is.False);
        Assert.That(sim.HasAvailableSwap(), Is.True);
    }

    [Test]
    public void TrySwap_WhenRejected_GridUnchanged()
    {
        var baseline = new Match3BoardSimulator();
        baseline.RandomInitNoMatches();

        var sim = new Match3BoardSimulator();
        var beforeSwap = new Match3BoardSimulator();

        bool foundReject = false;
        for (int r = 0; r < Match3BoardSimulator.Size; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size - 1; c++)
            {
                sim.CopyStateFrom(baseline);
                beforeSwap.CopyStateFrom(sim);
                bool ok = sim.TrySwap(r, c, r, c + 1);
                if (ok)
                    continue;

                AssertSameGrid(beforeSwap, sim, $"horizontal swap ({r},{c})");
                foundReject = true;
            }
        }

        for (int r = 0; r < Match3BoardSimulator.Size - 1; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size; c++)
            {
                sim.CopyStateFrom(baseline);
                beforeSwap.CopyStateFrom(sim);
                bool ok = sim.TrySwap(r, c, r + 1, c);
                if (ok)
                    continue;

                AssertSameGrid(beforeSwap, sim, $"vertical swap ({r},{c})");
                foundReject = true;
            }
        }

        Assert.That(foundReject, Is.True, "Expected at least one invalid adjacent swap on a random board.");
    }

    [Test]
    public void TryApplyFirstValidSwap_EndsWithNoMatch()
    {
        var sim = new Match3BoardSimulator();
        sim.RandomInitNoMatches();
        Assert.That(sim.TryApplyFirstValidSwap(), Is.True);
        Assert.That(sim.HasAnyMatch(), Is.False);
    }

    [Test]
    public void SetCell_And_CopyStateFrom_RoundTrip()
    {
        var a = new Match3BoardSimulator();
        a.SetCell(3, 3, 2);
        a.SetCell(3, 4, 3);

        var b = new Match3BoardSimulator();
        b.CopyStateFrom(a);

        Assert.That(b.GetCell(3, 3), Is.EqualTo(2));
        Assert.That(b.GetCell(3, 4), Is.EqualTo(3));
    }

    private static void AssertSameGrid(Match3BoardSimulator expected, Match3BoardSimulator actual, string message)
    {
        for (int r = 0; r < Match3BoardSimulator.Size; r++)
        {
            for (int c = 0; c < Match3BoardSimulator.Size; c++)
            {
                Assert.That(actual.GetCell(r, c), Is.EqualTo(expected.GetCell(r, c)),
                    $"{message} @ ({r},{c})");
                Assert.That(actual.IsCellLocked(r, c), Is.EqualTo(expected.IsCellLocked(r, c)),
                    $"{message} lock @ ({r},{c})");
            }
        }
    }
}
