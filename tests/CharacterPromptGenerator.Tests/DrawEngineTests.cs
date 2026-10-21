using CharacterPromptGenerator.Core.Drawing;

namespace CharacterPromptGenerator.Tests;

public class DrawEngineTests
{
    [Fact]
    public void Generate_NeverRepeatsEntryWithinCategory()
    {
        var lib = Lib.Small(5);
        for (var seed = 0; seed < 200; seed++)
        {
            var outcome = new DrawEngine(new Random(seed)).Generate(lib, [new DrawRequest("A", 5)]);
            var ids = outcome.Slots.Where(s => s.CategoryId == "A").Select(s => s.EntryId).ToList();
            Assert.Equal(5, ids.Count);
            Assert.Equal(5, ids.Distinct().Count());
            Assert.Empty(outcome.Warnings);
        }
    }

    [Fact]
    public void Generate_TreatsSameTextAsDuplicateEvenWithDifferentIds()
    {
        var lib = Lib.Small(2);
        lib.Entries.Add(new() { Id = "dup", CategoryId = "A", Text = "  詞1 " });
        var outcome = new DrawEngine(new Random(1)).Generate(lib, [new DrawRequest("A", 3)]);
        Assert.Equal(2, outcome.Slots.Count);
        Assert.Single(outcome.Warnings);
    }

    [Fact]
    public void Generate_Shortage_DrawsMaxAvailableAndWarns()
    {
        var lib = Lib.Small(3);
        var outcome = new DrawEngine(new Random(1)).Generate(lib, [new DrawRequest("A", 10)]);
        Assert.Equal(3, outcome.Slots.Count);
        Assert.Contains(outcome.Warnings, w => w.Contains("分類A") && w.Contains("10"));
    }

    [Fact]
    public void Generate_EmptyCategory_WarnsWithoutCrash()
    {
        var lib = Lib.Small(0);
        var outcome = new DrawEngine(new Random(1)).Generate(lib, [new DrawRequest("A", 2), new DrawRequest("B", 1)]);
        Assert.Single(outcome.Slots);
        Assert.Contains(outcome.Warnings, w => w.Contains("沒有可抽的詞條"));
    }

    [Fact]
    public void DisabledEntriesAndCategories_AreNeverDrawn()
    {
        var lib = Lib.Small(5);
        lib.Entries.Where(e => e.CategoryId == "A" && e.Id != "a3").ToList().ForEach(e => e.Enabled = false);
        lib.FindCategory("B")!.Enabled = false;
        for (var seed = 0; seed < 50; seed++)
        {
            var outcome = new DrawEngine(new Random(seed)).Generate(lib, [new DrawRequest("A", 3), new DrawRequest("B", 2)]);
            Assert.Equal(["a3"], outcome.Slots.Select(s => s.EntryId));
        }
    }

    [Fact]
    public void Reroll_KeepsLockedSlotsInPlace()
    {
        var lib = Lib.Small(20);
        var engine = new DrawEngine(new Random(7));
        var first = engine.Generate(lib, [new DrawRequest("A", 3), new DrawRequest("B", 1)]).Slots;
        first[1].Locked = true;
        var lockedId = first[1].EntryId;

        for (var i = 0; i < 50; i++)
        {
            var next = engine.Reroll(lib, [new DrawRequest("A", 3), new DrawRequest("B", 1)], first).Slots;
            Assert.Equal(lockedId, next[1].EntryId);
            Assert.True(next[1].Locked);
            Assert.Equal(3, next.Count(s => s.CategoryId == "A"));
            Assert.Equal(3, next.Where(s => s.CategoryId == "A").Select(s => s.EntryId).Distinct().Count());
            first = next;
        }
    }

    [Fact]
    public void Reroll_PrefersFreshEntries()
    {
        var lib = Lib.Small(6);
        var engine = new DrawEngine(new Random(3));
        var first = engine.Generate(lib, [new DrawRequest("A", 3)]).Slots;
        var next = engine.Reroll(lib, [new DrawRequest("A", 3)], first).Slots;
        Assert.Empty(first.Select(s => s.EntryId).Intersect(next.Select(s => s.EntryId)));
    }

    [Fact]
    public void Reroll_SingleCategory_LeavesOthersUntouched()
    {
        var lib = Lib.Small(20);
        var engine = new DrawEngine(new Random(5));
        var reqs = new[] { new DrawRequest("A", 2), new DrawRequest("B", 1) };
        var first = engine.Generate(lib, reqs).Slots;
        var bBefore = first.Single(s => s.CategoryId == "B").EntryId;
        var next = engine.Reroll(lib, reqs, first, onlyCategoryId: "A").Slots;
        Assert.Equal(bBefore, next.Single(s => s.CategoryId == "B").EntryId);
        Assert.Empty(first.Where(s => s.CategoryId == "A").Select(s => s.EntryId)
            .Intersect(next.Where(s => s.CategoryId == "A").Select(s => s.EntryId)));
    }

    [Fact]
    public void Unlock_ThenReroll_CanChangeTheSlot()
    {
        var lib = Lib.Small(2);
        var engine = new DrawEngine(new Random(1));
        var first = engine.Generate(lib, [new DrawRequest("A", 1)]).Slots;
        first[0].Locked = true;
        var locked = engine.Reroll(lib, [new DrawRequest("A", 1)], first).Slots;
        Assert.Equal(first[0].EntryId, locked[0].EntryId);
        locked[0].Locked = false;
        var unlocked = engine.Reroll(lib, [new DrawRequest("A", 1)], locked).Slots;
        Assert.NotEqual(first[0].EntryId, unlocked[0].EntryId);
    }

    [Fact]
    public void LockedSlot_SurvivesEntryDeletion()
    {
        var lib = Lib.Small(3);
        var engine = new DrawEngine(new Random(1));
        var slots = engine.Generate(lib, [new DrawRequest("B", 1)]).Slots;
        slots[0].Locked = true;
        lib.Entries.RemoveAll(e => e.CategoryId == "B");
        lib.Categories.RemoveAll(c => c.Id == "B");
        var next = engine.Reroll(lib, [new DrawRequest("B", 1)], slots).Slots;
        Assert.Single(next);
        Assert.Equal(slots[0].Text, next[0].Text);
    }

    [Fact]
    public void Formatter_IncludesCategoryAndEmoji()
    {
        var lib = Lib.Small(1);
        var slots = new DrawEngine(new Random(1)).Generate(lib, [new DrawRequest("A", 1), new DrawRequest("B", 2)]).Slots;
        var text = PromptFormatter.ToText(slots);
        Assert.Contains("分類A：詞1", text);
        Assert.Contains("🕯️ 蠟燭", text);
        Assert.Contains("🦋 飛蛾", text);
    }
}
