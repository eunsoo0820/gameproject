using System;

namespace Drift
{
    public enum Item { None, Fish, Water, Scrap, Fuel, PurifierPart, FishingRod }
    public enum TaskId { Food, Purifier, Fuel, Deck }
    public enum StoryStage { FindNote, DailyWork, Radio, Emergency, Complete }

    public struct ItemStack
    {
        public Item Item;
        public int Count;
    }

    // Pure rules: one owner per local session, independent from UI and scene objects.
    public sealed class DriftState
    {
        public const int SlotCount = 24;
        public const int HotbarCount = 8;
        public const int StackLimit = 20;
        private readonly ItemStack[] slots = new ItemStack[SlotCount];
        private readonly bool[] tasks = new bool[4];
        public StoryStage Stage { get; private set; } = StoryStage.FindNote;
        public float Hunger { get; private set; } = 100;
        public float Thirst { get; private set; } = 100;
        public float Health { get; private set; } = 100;
        public bool WestReached { get; private set; }
        public bool PurifierBroken { get; private set; }
        public bool PurifierRepaired { get; private set; }
        public int SelectedSlot { get; private set; }
        public bool IsDead => Health <= 0;
        public ItemStack GetSlot(int index) => slots[CheckedIndex(index)];
        private static int CheckedIndex(int index)
        {
            if (index < 0 || index >= SlotCount) throw new ArgumentOutOfRangeException(nameof(index));
            return index;
        }
        public void Select(int index) { SelectedSlot = CheckedIndex(index); }
        public bool IsDone(TaskId task) => tasks[(int)task];
        public int Count(Item item)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++) if (slots[i].Item == item) total += slots[i].Count;
            return total;
        }
        public bool TryAdd(Item item, int count = 1)
        {
            if (item <= Item.None || item > Item.FishingRod || count <= 0) return false;
            int capacity = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Item == item || slots[i].Count == 0) capacity += StackLimit - slots[i].Count;
            if (capacity < count) return false; // Never partially consume a world pickup.
            for (int pass = 0; pass < 2 && count > 0; pass++)
                for (int i = 0; i < slots.Length && count > 0; i++)
                {
                    bool match = pass == 0 ? slots[i].Item == item : slots[i].Count == 0;
                    if (!match) continue;
                    int amount = Math.Min(StackLimit - slots[i].Count, count);
                    slots[i].Item = item;
                    slots[i].Count += amount;
                    count -= amount;
                }
            return true;
        }
        public bool TryRemove(Item item, int count = 1)
        {
            if (item == Item.None || count <= 0 || Count(item) < count) return false;
            for (int i = 0; i < slots.Length && count > 0; i++)
            {
                if (slots[i].Item != item) continue;
                int take = Math.Min(count, slots[i].Count);
                slots[i].Count -= take;
                count -= take;
                if (slots[i].Count == 0) slots[i].Item = Item.None;
            }
            return true;
        }
        public bool TryTakeSelected(out Item item)
        {
            item = slots[SelectedSlot].Item;
            if (slots[SelectedSlot].Count == 0) return false;
            slots[SelectedSlot].Count--;
            if (slots[SelectedSlot].Count == 0) slots[SelectedSlot].Item = Item.None;
            return true;
        }
        public bool UseSelected()
        {
            Item item = slots[SelectedSlot].Item;
            if (item != Item.Fish && item != Item.Water) return false;
            if (!TryTakeSelected(out item)) return false;
            if (item == Item.Fish) Hunger = Math.Min(100, Hunger + 30);
            else Thirst = Math.Min(100, Thirst + 40);
            return true;
        }
        public void ReadNote()
        {
            if (Stage == StoryStage.FindNote) Stage = StoryStage.DailyWork;
        }
        public bool CompleteTask(TaskId task)
        {
            if (Stage != StoryStage.DailyWork || tasks[(int)task]) return false;
            tasks[(int)task] = true;
            bool complete = true;
            for (int i = 0; i < tasks.Length; i++) complete &= tasks[i];
            if (complete) Stage = StoryStage.Radio;
            return true;
        }
        public bool HearRadio(bool insideWheelhouse)
        {
            if (Stage != StoryStage.Radio || !insideWheelhouse) return false;
            Stage = StoryStage.Emergency;
            return true;
        }
        public void BreakPurifier()
        {
            if (Stage == StoryStage.Emergency && !PurifierRepaired) PurifierBroken = true;
        }
        public void SetHeading(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return;
            degrees = (degrees % 360 + 360) % 360;
            if (Stage != StoryStage.Emergency || Math.Abs(degrees - 270) > 7) return;
            WestReached = true;
            CheckFinished();
        }
        public bool RepairPurifier()
        {
            if (!PurifierBroken || !TryRemove(Item.PurifierPart)) return false;
            PurifierBroken = false;
            PurifierRepaired = true;
            CheckFinished();
            return true;
        }
        private void CheckFinished()
        {
            if (WestReached && PurifierRepaired) Stage = StoryStage.Complete;
        }
        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || IsDead) return;
            float deprivationSeconds = Math.Max(0, seconds - Math.Min(Hunger / .035f, Thirst / .055f));
            Hunger = Math.Max(0, Hunger - seconds * 0.035f);
            Thirst = Math.Max(0, Thirst - seconds * 0.055f);
            if (deprivationSeconds > 0) Health = Math.Max(0, Health - deprivationSeconds * 2);
        }
    }
}
