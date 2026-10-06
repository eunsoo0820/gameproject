using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Drift
{
    public enum Control { Inventory, Interact, Drop, Sprint, Forward, Back, Left, Right, Jump, Crouch }
    public sealed class DriftSettings
    {
        private static readonly Key[] Defaults = { Key.E, Key.F, Key.Q, Key.LeftShift, Key.W, Key.S, Key.A, Key.D, Key.Space, Key.LeftCtrl };
        private readonly Key[] keys = new Key[Defaults.Length];
        public bool Korean { get; private set; }
        public int Volume { get; private set; }
        public bool VoiceEnabled { get; private set; }
        public bool ChatEnabled { get; private set; }
        public DriftSettings()
        {
            Korean = PlayerPrefs.GetInt("drift.language", 0) == 0;
            Volume = Mathf.Clamp(PlayerPrefs.GetInt("drift.volume", 75), 0, 100);
            VoiceEnabled = PlayerPrefs.GetInt("drift.voice", 1) != 0;
            ChatEnabled = PlayerPrefs.GetInt("drift.chat", 1) != 0;
            bool invalid = false;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = (Key)PlayerPrefs.GetInt("drift.key." + i, (int)Defaults[i]);
                if (!IsAllowed(keys[i])) invalid = true;
                for (int j = 0; j < i; j++) if (keys[j] == keys[i]) invalid = true;
            }
            if (invalid) Array.Copy(Defaults, keys, keys.Length);
            AudioListener.volume = Volume / 100f;
        }
        public Key Binding(Control control) => keys[(int)control];
        public bool Held(Control control) => Keyboard.current != null && Keyboard.current[Binding(control)].isPressed;
        public bool Pressed(Control control) => Keyboard.current != null && Keyboard.current[Binding(control)].wasPressedThisFrame;
        public string Text(string ko, string en) => Korean ? ko : en;
        public void SetLanguage(bool korean) { Korean = korean; PlayerPrefs.SetInt("drift.language", korean ? 0 : 1); Save(); }
        public void SetVolume(float volume) { Volume = Mathf.Clamp(Mathf.RoundToInt(volume), 0, 100); AudioListener.volume = Volume / 100f; PlayerPrefs.SetInt("drift.volume", Volume); }
        public void SetVoice(bool enabled) { VoiceEnabled = enabled; PlayerPrefs.SetInt("drift.voice", enabled ? 1 : 0); Save(); }
        public void SetChat(bool enabled) { ChatEnabled = enabled; PlayerPrefs.SetInt("drift.chat", enabled ? 1 : 0); Save(); }
        public bool Rebind(Control control, Key key)
        {
            if (!IsAllowed(key)) return false;
            for (int i = 0; i < keys.Length; i++) if (i != (int)control && keys[i] == key) return false;
            keys[(int)control] = key;
            PlayerPrefs.SetInt("drift.key." + (int)control, (int)key);
            Save();
            return true;
        }
        private static bool IsAllowed(Key key) => Enum.IsDefined(typeof(Key), key) && key != Key.None && key != Key.Escape && key != Key.Enter && key != Key.Tab && !(key >= Key.Digit1 && key <= Key.Digit0);
        public void ResetBindings()
        {
            Array.Copy(Defaults, keys, keys.Length);
            for (int i = 0; i < keys.Length; i++) PlayerPrefs.SetInt("drift.key." + i, (int)keys[i]);
            Save();
        }
        public void Save() => PlayerPrefs.Save();
        public string ItemName(Item item)
        {
            switch (item)
            {
                case Item.Fish: return Text("생선", "Fish");
                case Item.Water: return Text("식수", "Water");
                case Item.Scrap: return Text("수리 자재", "Scrap");
                case Item.Fuel: return Text("연료", "Fuel");
                case Item.PurifierPart: return Text("정수기 부품", "Filter part");
                case Item.FishingRod: return Text("낚싯대", "Fishing rod");
                default: return "—";
            }
        }
        public string ControlName(Control control)
        {
            string[] ko = { "인벤토리", "상호작용", "아이템 버리기", "달리기", "앞으로", "뒤로", "왼쪽", "오른쪽", "점프", "웅크리기" };
            string[] en = { "Inventory", "Interact", "Drop item", "Sprint", "Forward", "Back", "Left", "Right", "Jump", "Crouch" };
            return Korean ? ko[(int)control] : en[(int)control];
        }
    }
}
