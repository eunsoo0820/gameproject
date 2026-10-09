using System;

namespace Drift
{
    public enum Item { None, Fish, Water, Scrap, Fuel, PurifierPart, FishingRod }
    public enum TaskId { Food, Purifier, Fuel, Deck }
    public enum StoryStage { FindNote, DailyWork, Radio, SteerWest, GullStrike, FindPurifierPart, RepairPurifier, Complete }
    public enum CharacterId { Daniel, Charles, Alice, Carl, Emma, Mina, Mason }

    public sealed class CharacterProfile
    {
        public readonly CharacterId Id;
        public readonly string Name;
        public readonly int Thirst, Hunger, Health, Inventory, Skill, Breath, Speed;
        public CharacterProfile(CharacterId id, string name, int thirst, int hunger, int health, int inventory, int skill, int breath, int speed)
        { Id = id; Name = name; Thirst = thirst; Hunger = hunger; Health = health; Inventory = inventory; Skill = skill; Breath = breath; Speed = speed; }
        public static readonly CharacterProfile[] All = {
            new CharacterProfile(CharacterId.Daniel, "Daniel", 60, 70, 90, 7, 20, 12, 13),
            new CharacterProfile(CharacterId.Charles, "Charles", 100, 100, 100, 5, 10, 10, 10),
            new CharacterProfile(CharacterId.Alice, "Alice", 100, 100, 70, 10, 3, 20, 10),
            new CharacterProfile(CharacterId.Carl, "Carl", 150, 150, 70, 5, 4, 10, 20),
            new CharacterProfile(CharacterId.Emma, "Emma", 80, 80, 80, 7, 13, 13, 13),
            new CharacterProfile(CharacterId.Mina, "Mina · Hardcore", 50, 50, 50, 2, 25, 5, 5),
            new CharacterProfile(CharacterId.Mason, "Mason", 100, 150, 180, 3, 5, 8, 7)
        };
        public static CharacterProfile Get(CharacterId id) => All[(int)id];
    }

    public struct ItemStack { public Item Item; public int Count; }

    // Pure rules: one owner per local session, independent from UI and scene objects.
    public sealed class DriftState
    {
        public const int SlotCount = 24;
        public const int HotbarCount = 8;
        public const int StackLimit = 20;
        private readonly ItemStack[] slots = new ItemStack[SlotCount];
        private readonly bool[] tasks = new bool[4];
        public CharacterProfile Character { get; }
        public int InventorySlots => Character.Inventory;
        public int HotbarSlots => Math.Min(HotbarCount, InventorySlots);
        public StoryStage Stage { get; private set; } = StoryStage.FindNote;
        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Health { get; private set; }
        public float HungerMax => Character.Hunger;
        public float ThirstMax => Character.Thirst;
        public float HealthMax => Character.Health;
        public float Breath => breathRemaining;
        public float BreathMax => Character.Breath;
        public bool WestReached { get; private set; }
        public bool PurifierBroken { get; private set; }
        public bool PurifierRepaired { get; private set; }
        public bool PurifierPartSpawned { get; private set; }
        public int SelectedSlot { get; private set; }
        public bool IsDead => Health <= 0;
        private float breathRemaining;

        public DriftState(CharacterId character = CharacterId.Charles)
        {
            Character = CharacterProfile.Get(character);
            Hunger = Character.Hunger; Thirst = Character.Thirst; Health = Character.Health; breathRemaining = Character.Breath;
        }
        public ItemStack GetSlot(int index) => slots[CheckedIndex(index)];
        private static int CheckedIndex(int index)
        {
            if (index < 0 || index >= SlotCount) throw new ArgumentOutOfRangeException(nameof(index));
            return index;
        }
        public void Select(int index) { if (index >= 0 && index < HotbarSlots) SelectedSlot = index; }
        public bool IsDone(TaskId task) => tasks[(int)task];
        public int Count(Item item)
        {
            int total = 0;
            for (int i = 0; i < InventorySlots; i++) if (slots[i].Item == item) total += slots[i].Count;
            return total;
        }
        public bool TryAdd(Item item, int count = 1)
        {
            if (item <= Item.None || item > Item.FishingRod || count <= 0) return false;
            int capacity = 0;
            for (int i = 0; i < InventorySlots; i++)
                if (slots[i].Item == item || slots[i].Count == 0) capacity += StackLimit - slots[i].Count;
            if (capacity < count) return false;
            for (int pass = 0; pass < 2 && count > 0; pass++)
                for (int i = 0; i < InventorySlots && count > 0; i++)
                {
                    bool match = pass == 0 ? slots[i].Item == item : slots[i].Count == 0;
                    if (!match) continue;
                    int amount = Math.Min(StackLimit - slots[i].Count, count);
                    slots[i].Item = item; slots[i].Count += amount; count -= amount;
                }
            return true;
        }
        public bool TryRemove(Item item, int count = 1)
        {
            if (item == Item.None || count <= 0 || Count(item) < count) return false;
            for (int i = 0; i < InventorySlots && count > 0; i++)
            {
                if (slots[i].Item != item) continue;
                int take = Math.Min(count, slots[i].Count); slots[i].Count -= take; count -= take;
                if (slots[i].Count == 0) slots[i].Item = Item.None;
            }
            return true;
        }
        public bool TryTakeSelected(out Item item)
        {
            item = slots[SelectedSlot].Item;
            if (SelectedSlot >= InventorySlots || slots[SelectedSlot].Count == 0) return false;
            slots[SelectedSlot].Count--; if (slots[SelectedSlot].Count == 0) slots[SelectedSlot].Item = Item.None;
            return true;
        }
        public bool UseSelected()
        {
            Item item = slots[SelectedSlot].Item;
            if (item != Item.Fish && item != Item.Water) return false;
            if (!TryTakeSelected(out item)) return false;
            if (item == Item.Fish) Hunger = Math.Min(HungerMax, Hunger + 30);
            else Thirst = Math.Min(ThirstMax, Thirst + 40);
            return true;
        }
        public void RestoreThirst() => Thirst = ThirstMax;
        public void TakeDamage(float damage) { if (damage > 0) Health = Math.Max(0, Health - damage); }
        public void TickBreath(float seconds, bool underwater)
        {
            if (!underwater) { breathRemaining = BreathMax; return; }
            breathRemaining = Math.Max(0, breathRemaining - seconds);
            if (breathRemaining <= 0) TakeDamage(seconds * 5);
        }
        public void AcquirePurifierPart()
        {
            if (Stage == StoryStage.FindPurifierPart && Count(Item.PurifierPart) > 0) Stage = StoryStage.RepairPurifier;
        }
        public void ReadNote() { if (Stage == StoryStage.FindNote) Stage = StoryStage.DailyWork; }
        public bool CompleteTask(TaskId task)
        {
            if (Stage != StoryStage.DailyWork || tasks[(int)task]) return false;
            tasks[(int)task] = true;
            bool complete = true; for (int i = 0; i < tasks.Length; i++) complete &= tasks[i];
            if (complete) Stage = StoryStage.Radio;
            return true;
        }
        public bool HearRadio(bool insideWheelhouse)
        {
            if (Stage != StoryStage.Radio || !insideWheelhouse) return false;
            Stage = StoryStage.SteerWest; return true;
        }
        public void BeginGullStrike() { if (Stage == StoryStage.SteerWest && WestReached) Stage = StoryStage.GullStrike; }
        public void BreakPurifier()
        {
            if (Stage == StoryStage.GullStrike && !PurifierRepaired) { PurifierBroken = true; Stage = StoryStage.FindPurifierPart; }
        }
        public bool SpawnPurifierPart()
        {
            if (Stage != StoryStage.FindPurifierPart || PurifierPartSpawned) return false;
            PurifierPartSpawned = true; return true;
        }
        public void SetHeading(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return;
            degrees = (degrees % 360 + 360) % 360;
            if (Stage != StoryStage.SteerWest || Math.Abs(degrees - 270) > 7) return;
            WestReached = true; Stage = StoryStage.GullStrike;
        }
        public bool RepairPurifier()
        {
            if (Stage != StoryStage.RepairPurifier || !TryRemove(Item.PurifierPart)) return false;
            PurifierBroken = false; PurifierRepaired = true; Stage = StoryStage.Complete; return true;
        }
        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || IsDead) return;
            float hungerLoss = seconds * .035f, thirstLoss = seconds * .055f;
            float deprivationSeconds = Math.Max(0, seconds - Math.Min(Hunger / .035f, Thirst / .055f));
            Hunger = Math.Max(0, Hunger - hungerLoss); Thirst = Math.Max(0, Thirst - thirstLoss);
            if (deprivationSeconds > 0) TakeDamage(deprivationSeconds * 2);
        }
    }
}
