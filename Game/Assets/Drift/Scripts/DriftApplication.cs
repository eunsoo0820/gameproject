using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Drift
{
    public sealed class DriftApplication : MonoBehaviour
    {
        [SerializeField] private Material worldMaterial;
        [SerializeField] private TMP_FontAsset uiFont;
        public DriftSettings Settings { get; private set; }
        public DriftState State { get; private set; }
        public DriftWorld World { get; private set; }
        public ScreenId Screen { get; private set; }
        private DriftUI ui;
        private DriftView view;
        private DriftAudio audioSystem;
        private ScreenId settingsReturn;
        private Control? rebinding;
        private readonly Queue<string> chat = new Queue<string>();
        private readonly StringBuilder quest = new StringBuilder(512);
        private float fadeRemaining, refreshTimer, radioRemaining, gullRemaining = -1, fishCooldown;
        private bool atHelm;
        public string ChatHistory => string.Join("\n", chat);
        private string T(string ko, string en) => Settings.Text(ko, en);

        private void Awake()
        {
            if (worldMaterial == null || uiFont == null)
            {
                Debug.LogError("Drift setup is incomplete. Run Drift > Create or Open Game.", this); enabled = false; return;
            }
            Settings = new DriftSettings();
            World = new DriftWorld(transform, worldMaterial, uiFont);
            view = new GameObject("Invisible Test Viewpoint").AddComponent<DriftView>();
            view.Initialize(World.Ship, Settings);
            audioSystem = new DriftAudio(transform);
            ui = new DriftUI(this, uiFont);
            EndVoyage();
        }
        public void Navigate(ScreenId screen)
        {
            if (Screen == ScreenId.Settings && screen != ScreenId.Settings) Settings.Save();
            Screen = screen;
            Cursor.lockState = screen == ScreenId.Playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = screen != ScreenId.Playing;
            ui.Show(screen); ui.RefreshHud();
        }
        public void CreateRoom() => Navigate(ScreenId.Room);
        public void StartVoyage()
        {
            ui.ClearNotice();
            State = new DriftState(); World.Reset(); view.ResetView(); atHelm = false;
            fadeRemaining = 3; radioRemaining = 0; gullRemaining = -1; fishCooldown = 0; chat.Clear();
            audioSystem.Stop(); audioSystem.StartSea(); World.RefreshMarkers(State);
            Navigate(ScreenId.Playing); ui.SetFade(1);
        }
        public void EndVoyage()
        {
            ui.ClearNotice();
            atHelm = false; State = null; audioSystem.Stop(); fadeRemaining = 0; radioRemaining = 0; gullRemaining = -1;
            World.Reset(); view.Preview(); ui.SetFade(0); Navigate(ScreenId.Menu);
        }
        public void OpenSettings() { settingsReturn = Screen; rebinding = null; Navigate(ScreenId.Settings); }
        public void CloseSettings() { rebinding = null; Settings.Save(); Navigate(settingsReturn); }
        public void BeginRebind(Control control)
        {
            rebinding = control;
            ui.RebindMessage(Settings.ControlName(control) + T(": 새 키를 누르세요. ESC 취소", ": press a new key. ESC cancels"));
        }
        private void Update()
        {
            if (ui == null) return;
            Keyboard keyboard = Keyboard.current;
            if (rebinding.HasValue && keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { rebinding = null; ui.Show(ScreenId.Settings); return; }
                foreach (var key in keyboard.allKeys)
                {
                    if (!key.wasPressedThisFrame) continue;
                    if (Settings.Rebind(rebinding.Value, key.keyCode)) { rebinding = null; ui.Show(ScreenId.Settings); }
                    else ui.RebindMessage(T("이미 사용 중이거나 UI 예약 키입니다. 다른 키를 선택하세요.", "Key is in use or reserved for UI. Choose another key."));
                    break;
                }
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && Screen != ScreenId.Dialogue)
            {
                if (Screen == ScreenId.Playing) Navigate(ScreenId.Pause);
                else if (Screen == ScreenId.Pause || Screen == ScreenId.Inventory || Screen == ScreenId.Chat) Navigate(ScreenId.Playing);
                else if (Screen == ScreenId.Settings) CloseSettings();
                else if (Screen != ScreenId.Menu && Screen != ScreenId.Dead) Navigate(ScreenId.Menu);
                return;
            }
            if (Screen == ScreenId.Dialogue)
            {
                radioRemaining -= Time.unscaledDeltaTime;
                if (radioRemaining <= 0)
                {
                    audioSystem.Disconnect(); gullRemaining = 2; World.StartGullStrike(); Navigate(ScreenId.Playing);
                }
            }
            if (Screen == ScreenId.Inventory && Settings.Pressed(Control.Inventory)) { Navigate(ScreenId.Playing); return; }
            if (Screen == ScreenId.Playing && State != null)
            {
                float dt = Time.deltaTime;
                if (fadeRemaining > 0)
                {
                    fadeRemaining = Mathf.Max(0, fadeRemaining - dt); ui.SetFade(fadeRemaining / 3);
                }
                else
                {
                    if (Settings.Pressed(Control.Inventory)) { Navigate(ScreenId.Inventory); return; }
                    if (keyboard != null && keyboard.enterKey.wasPressedThisFrame && Settings.ChatEnabled) { Navigate(ScreenId.Chat); return; }
                    view.Tick(dt, atHelm);
                    if (atHelm)
                    {
                        float rudder = (Settings.Held(Control.Right) ? 1 : 0) - (Settings.Held(Control.Left) ? 1 : 0);
                        World.Steer(rudder, dt); State.SetHeading(World.Heading);
                        ui.SetPrompt(T("조타 중  ·  ", "At helm  ·  ") + Settings.Binding(Control.Left) + " / " + Settings.Binding(Control.Right) + T(" 회전  ·  ", " turn  ·  ") + Settings.Binding(Control.Interact) + T(" 놓기", " release"));
                        if (Settings.Pressed(Control.Interact)) atHelm = false;
                    }
                    else
                    {
                        DriftInteractable target = view.Target();
                        ui.SetPrompt(target != null ? "[" + Settings.Binding(Control.Interact) + "] " + StationName(target) : "");
                        if (target != null && Settings.Pressed(Control.Interact)) Interact(target);
                    }
                    if (Settings.Pressed(Control.Drop)) DropItem();
                    if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) UseItem();
                    if (keyboard != null)
                        for (int i = 0; i < DriftState.HotbarCount; i++)
                            if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) State.Select(i);
                    if (Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0)
                    {
                        int direction = Mouse.current.scroll.ReadValue().y > 0 ? -1 : 1;
                        State.Select((Mathf.Min(State.SelectedSlot, 7) + direction + 8) % 8);
                    }
                    State.Tick(dt); if (State.IsDead) Navigate(ScreenId.Dead);
                }
                fishCooldown = Mathf.Max(0, fishCooldown - dt);
                World.Tick(dt);
                if (gullRemaining >= 0)
                {
                    gullRemaining -= dt;
                    if (gullRemaining <= 0) { gullRemaining = -1; State.BreakPurifier(); ui.Toast(T("갈매기가 정수기 부품을 망가뜨렸습니다!", "A seagull damaged the purifier's main part!")); }
                }
                World.RefreshMarkers(State);
                audioSystem.SetAlarm(State.Stage == StoryStage.Radio);
            }
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0) { refreshTimer = .2f; ui.RefreshHud(); }
        }
        public string StationName(DriftInteractable target)
        {
            switch (target.Kind)
            {
                case Station.Note: return T("오늘의 작업 읽기", "Read today's work");
                case Station.Fishing: return T("낚시하기", "Fish");
                case Station.Barrel: return T("빗물통에서 물 받기", "Collect rainwater");
                case Station.Purifier: return State != null && State.PurifierBroken ? T("정수기 수리", "Repair purifier") : T("정수기 점검 / 식수 받기", "Inspect purifier / collect water");
                case Station.Engine: return T("엔진 연료 확인 / 보충", "Check / refill engine fuel");
                case Station.DeckRepair: return T("갑판 손상부 수리", "Repair deck");
                case Station.Radio: return T("낡은 라디오", "Old radio");
                case Station.Helm: return T("방향타 잡기", "Take helm");
                default: return Settings.ItemName(target.PickupItem) + T(" 줍기", " · pick up");
            }
        }
        private void Interact(DriftInteractable target)
        {
            switch (target.Kind)
            {
                case Station.Note: State.ReadNote(); ui.Toast(T("오늘의 작업을 퀘스트에 등록했습니다.", "Today's work added to your objectives.")); break;
                case Station.Pickup:
                    if (State.TryAdd(target.PickupItem)) { ui.Toast(Settings.ItemName(target.PickupItem) + T(" 획득", " collected")); World.Collect(target); }
                    else Full(); break;
                case Station.Fishing:
                    if (State.Count(Item.FishingRod) == 0) { ui.Toast(T("갑판의 낚싯대를 먼저 주우세요.", "Pick up the fishing rod on deck first.")); break; }
                    if (fishCooldown > 0) { ui.Toast(T("물고기가 다시 모이기를 기다리세요.", "Wait for the fish to return.")); break; }
                    if (State.TryAdd(Item.Fish)) { State.CompleteTask(TaskId.Food); fishCooldown = 4; ui.Toast(T("생선을 잡았습니다. 인벤토리에서 먹을 수 있습니다.", "Fish caught. Eat it from your inventory.")); } else Full(); break;
                case Station.Barrel:
                    if (State.TryAdd(Item.Water)) ui.Toast(T("물을 받았습니다.", "Water collected.")); else Full(); break;
                case Station.Purifier:
                    if (State.PurifierBroken)
                    {
                        ui.Toast(State.RepairPurifier() ? T("정수기를 수리했습니다.", "Purifier repaired.") : T("정수기 부품이 필요합니다. 갑판의 흰색 상자를 찾으세요.", "A filter part is required. Find the white box on deck."));
                    }
                    else { State.CompleteTask(TaskId.Purifier); if (State.TryAdd(Item.Water)) ui.Toast(T("정수기가 정상입니다. 식수를 받았습니다.", "Purifier checked. Drinking water collected.")); else Full(); } break;
                case Station.Engine:
                    if (!WorkReady(TaskId.Fuel)) break;
                    if (State.TryRemove(Item.Fuel)) { State.CompleteTask(TaskId.Fuel); ui.Toast(T("엔진 연료 확인 완료.", "Engine refuelled and checked.")); }
                    else ui.Toast(T("엔진 옆의 연료통을 먼저 주우세요.", "Pick up the fuel container beside the engine.")); break;
                case Station.DeckRepair:
                    if (!WorkReady(TaskId.Deck)) break;
                    if (State.TryRemove(Item.Scrap)) { State.CompleteTask(TaskId.Deck); ui.Toast(T("갑판 손상부를 수리했습니다.", "Deck damage repaired.")); }
                    else ui.Toast(T("수리 자재가 필요합니다. 갑판의 상자를 찾으세요.", "Scrap is required. Look for supplies on deck.")); break;
                case Station.Radio:
                    if (State.HearRadio(World.InsideWheelhouse(view.transform.position)))
                    { radioRemaining = 5; audioSystem.Radio(); Navigate(ScreenId.Dialogue); }
                    else ui.Toast(T("무전은 조용합니다. 오늘의 작업부터 확인하세요.", "The radio is silent. Check today's work first.")); break;
                case Station.Helm: atHelm = true; break;
            }
        }
        private bool WorkReady(TaskId task)
        {
            if (State.Stage == StoryStage.FindNote) { ui.Toast(T("먼저 조타실의 작업 종이를 읽으세요.", "Read the work note in the wheelhouse first.")); return false; }
            if (State.IsDone(task)) { ui.Toast(T("이미 완료한 작업입니다.", "This task is already complete.")); return false; }
            return State.Stage == StoryStage.DailyWork;
        }
        private void Full() => ui.Toast(T("인벤토리가 가득 찼습니다.", "Inventory is full."));
        public void UseItem()
        {
            if (State != null) ui.Toast(State.UseSelected() ? T("아이템을 사용했습니다.", "Item used.") : T("생선이나 식수를 선택하세요.", "Select fish or water."));
        }
        public void DropItem()
        {
            if (State == null) return;
            if (!World.CanDrop) { ui.Toast(T("갑판의 떨어진 아이템을 먼저 주워 주세요.", "Collect some dropped items first.")); return; }
            if (!State.TryTakeSelected(out Item item)) return;
            Vector3 direction = view.transform.forward;
            Vector3 origin = view.transform.position + Vector3.up * .45f;
            float distance = 1.2f;
            if (Physics.Raycast(origin, direction, out var obstacle, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(.1f, obstacle.distance - .25f);
            Vector3 local = World.Ship.InverseTransformPoint(view.transform.position + direction * distance);
            local.x = Mathf.Clamp(local.x, -5.3f, 5.3f); local.z = Mathf.Clamp(local.z, -14, 14); local.y = 1.2f;
            World.AddPickup(item, local, true);
        }
        public void SendLocalChat(string message)
        {
            if (!Settings.ChatEnabled || string.IsNullOrWhiteSpace(message)) return;
            message = message.Replace("<", "‹").Replace(">", "›").Replace("\n", " ").Replace("\r", " ");
            if (message.Length > 160) message = message.Substring(0, 160);
            if (chat.Count >= 8) chat.Dequeue(); chat.Enqueue(T("나: ", "You: ") + message); ui.Show(ScreenId.Chat);
        }
        public string QuestText
        {
            get
            {
                if (State == null) return "";
                quest.Clear(); quest.AppendLine(T("오늘의 항해", "VOYAGE OBJECTIVES")); quest.AppendLine();
                switch (State.Stage)
                {
                    case StoryStage.FindNote: quest.Append(T("조타실 벽의 작업 종이를 읽으세요.", "Read the work note on the wheelhouse wall.")); break;
                    case StoryStage.DailyWork:
                        QuestLine(State.IsDone(TaskId.Food), T("식량 확보", "Secure food"));
                        QuestLine(State.IsDone(TaskId.Purifier), T("정수기 점검", "Inspect purifier"));
                        QuestLine(State.IsDone(TaskId.Fuel), T("엔진 연료 확인", "Check engine fuel"));
                        QuestLine(State.IsDone(TaskId.Deck), T("갑판 손상부 수리", "Repair deck damage")); break;
                    case StoryStage.Radio:
                        quest.Append(T("! 경보\n조타실로 이동해 라디오를 확인하세요.\n집결: ", "! ALARM\nGather in the wheelhouse and use the radio.\nGathered: "));
                        quest.Append(World.InsideWheelhouse(view.transform.position) ? "1 / 1" : "0 / 1"); break;
                    case StoryStage.Emergency:
                        QuestLine(State.WestReached, T("키를 서쪽 270°로 돌리세요", "Turn the helm west, 270°"));
                        if (State.PurifierBroken || State.PurifierRepaired) QuestLine(State.PurifierRepaired, T("정수기 수리", "Repair the purifier")); break;
                    case StoryStage.Complete: quest.Append(T("모든 작업을 완료했습니다.\n서쪽을 향한 항해가 시작됩니다.", "All tasks complete.\nYour voyage west begins.")); break;
                }
                return quest.ToString();
            }
        }
        private void QuestLine(bool done, string text) { quest.Append(done ? T("[완료] ", "[Done] ") : "○ "); quest.AppendLine(text); }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && ui != null && Screen == ScreenId.Playing) Navigate(ScreenId.Pause);
        }
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; audioSystem?.Stop(); }
        private void OnEnable()
        {
            if (ui != null && State != null) { audioSystem.StartSea(); Navigate(Screen); }
        }
        private void OnDestroy() { ui?.Dispose(); audioSystem?.Dispose(); World?.Dispose(); Settings?.Save(); }
    }
}
