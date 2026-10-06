using System;
using Drift;

internal static class DriftRulesTests
{
    private static int passed;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static DriftState AtRadio()
    {
        var state = new DriftState(); state.ReadNote();
        foreach (TaskId task in Enum.GetValues<TaskId>()) state.CompleteTask(task);
        return state;
    }
    private static DriftState Emergency()
    {
        var state = AtRadio(); state.HearRadio(true); return state;
    }
    public static void Main()
    {
        Test("Cannot advance before reading work note", () => {
            var state = new DriftState(); Check(!state.CompleteTask(TaskId.Food), "Task activated early");
            Check(!state.HearRadio(true), "Radio activated early"); Check(state.Stage == StoryStage.FindNote, "Stage changed");
        });
        Test("All four tasks required, duplicates harmless", () => {
            var state = new DriftState(); state.ReadNote();
            state.CompleteTask(TaskId.Food); state.CompleteTask(TaskId.Purifier); state.CompleteTask(TaskId.Fuel);
            Check(!state.CompleteTask(TaskId.Food) && state.Stage == StoryStage.DailyWork, "Duplicate advanced story");
            state.CompleteTask(TaskId.Deck); Check(state.Stage == StoryStage.Radio, "Alarm not reached");
        });
        Test("Radio requires wheelhouse presence", () => {
            var state = AtRadio(); Check(!state.HearRadio(false), "Outside radio interaction accepted");
            Check(state.HearRadio(true) && !state.HearRadio(true), "Radio must trigger once");
        });
        Test("Purifier cannot be repaired before bird damage", () => {
            var state = Emergency(); state.TryAdd(Item.PurifierPart); Check(!state.RepairPurifier(), "Repair accepted before damage");
            Check(state.Count(Item.PurifierPart) == 1, "Part consumed early");
        });
        Test("West alone does not complete both objectives", () => {
            var state = Emergency(); state.SetHeading(270); Check(state.WestReached && state.Stage == StoryStage.Emergency, "Repair bypassed");
        });
        Test("West then repair completes story", () => {
            var state = Emergency(); state.SetHeading(270); state.BreakPurifier();
            Check(!state.RepairPurifier(), "Repair without part"); state.TryAdd(Item.PurifierPart);
            Check(state.RepairPurifier() && state.Stage == StoryStage.Complete, "Story did not complete");
            Check(state.Count(Item.PurifierPart) == 0 && !state.RepairPurifier(), "Part not consumed or duplicate repair");
        });
        Test("Repair then west also completes story", () => {
            var state = Emergency(); state.BreakPurifier(); state.TryAdd(Item.PurifierPart); state.RepairPurifier();
            Check(state.Stage == StoryStage.Emergency, "West bypassed"); state.SetHeading(-90);
            Check(state.Stage == StoryStage.Complete, "Negative equivalent heading failed");
        });
        Test("East is not west and angular tolerance is bounded", () => {
            var state = Emergency(); state.SetHeading(90); state.SetHeading(278); Check(!state.WestReached, "Wrong heading accepted");
            state.SetHeading(630); Check(state.WestReached, "Wrapped west heading failed");
        });
        Test("Item stacking conserves quantity", () => {
            var state = new DriftState(); Check(state.TryAdd(Item.Fish, 41), "Add failed");
            Check(state.GetSlot(0).Count == 20 && state.GetSlot(1).Count == 20 && state.GetSlot(2).Count == 1, "Incorrect stack split");
        });
        Test("Full inventory rejection is atomic", () => {
            var state = new DriftState(); state.TryAdd(Item.Fish, 479);
            Check(!state.TryAdd(Item.Fish, 2) && state.Count(Item.Fish) == 479, "Partial insertion on failure");
            Check(state.TryAdd(Item.Fish) && !state.TryAdd(Item.Water), "Capacity boundary wrong");
        });
        Test("Insufficient removal is atomic", () => {
            var state = new DriftState(); state.TryAdd(Item.Fuel, 2); Check(!state.TryRemove(Item.Fuel, 3), "Overdraw accepted");
            Check(state.Count(Item.Fuel) == 2, "Partial removal occurred");
        });
        Test("Drop removes chosen slot, not first matching item", () => {
            var state = new DriftState(); state.TryAdd(Item.Fish, 21); state.Select(1); state.TryTakeSelected(out var item);
            Check(item == Item.Fish && state.GetSlot(0).Count == 20 && state.GetSlot(1).Item == Item.None, "Wrong slot removed");
        });
        Test("Zero, negative and empty item inputs rejected", () => {
            var state = new DriftState(); Check(!state.TryAdd(Item.None) && !state.TryAdd(Item.Fish, 0) && !state.TryRemove(Item.Fish, -1), "Invalid input accepted");
        });
        Test("Consumption restores correct stat and consumes once", () => {
            var state = new DriftState(); state.Tick(100); float thirst = state.Thirst;
            state.TryAdd(Item.Fish); state.UseSelected(); Check(state.Hunger == 100 && state.Thirst == thirst, "Wrong stat restored");
            Check(!state.UseSelected(), "Empty slot consumed"); state.TryAdd(Item.Water); state.UseSelected(); Check(state.Thirst == 100, "Water failed");
        });
        Test("Tools cannot be eaten", () => {
            var state = new DriftState(); state.TryAdd(Item.FishingRod); Check(!state.UseSelected() && state.Count(Item.FishingRod) == 1, "Tool consumed");
        });
        Test("Survival values clamp at zero", () => {
            var state = new DriftState(); state.Tick(100000); Check(state.Health == 0 && state.Hunger == 0 && state.Thirst == 0 && state.IsDead, "Negative survival values");
        });
        Test("Invalid time does not poison survival values", () => {
            var state = new DriftState(); state.Tick(float.NaN); state.Tick(float.PositiveInfinity); state.Tick(-1);
            Check(state.Health == 100 && state.Thirst == 100, "Invalid time altered state");
        });
        Test("New session does not inherit previous state", () => {
            var old = AtRadio(); old.TryAdd(Item.Fish); var fresh = new DriftState();
            Check(fresh.Stage == StoryStage.FindNote && fresh.Count(Item.Fish) == 0 && fresh.Health == 100, "Session state leaked");
        });
        Test("Invalid heading cannot satisfy navigation", () => {
            var state = Emergency(); state.SetHeading(float.NaN); state.SetHeading(float.PositiveInfinity);
            Check(!state.WestReached, "Non-finite heading accepted");
        });
        Test("Crossing depletion threshold damages only elapsed starvation time", () => {
            var state = new DriftState(); state.Tick(100 / .055f + 1);
            Check(Math.Abs(state.Health - 98) < .01f, "Damage applied before thirst depleted");
        });
        Console.WriteLine($"{passed} behavioral tests passed.");
    }
}
