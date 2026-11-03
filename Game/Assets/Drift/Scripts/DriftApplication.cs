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
        private float stormDelay, stormRemaining, stormStrikeCheck;
        private float floodEventDelay, floodEventRemaining, floodDamageTimer;
        private bool atHelm, floodEventScheduled, floodEventActive, stormEventCompleted;
        public CharacterId SelectedCharacter { get; private set; } = CharacterId.Charles;
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
        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
        public void SelectCharacter(CharacterId id)
        {
            if (id == CharacterId.Mina && !Settings.MinaUnlocked) return;
            SelectedCharacter = id;
        }
        public void StartVoyage()
        {
            ui.ClearNotice();
            State = new DriftState(SelectedCharacter); World.Reset(); view.ResetView(); view.SetCharacterSpeed(State.Character.Speed); atHelm = false;
            floodEventDelay = 0; floodEventRemaining = 0; floodDamageTimer = 0;
            floodEventScheduled = false; floodEventActive = false; stormEventCompleted = false;
            fadeRemaining = 3; radioRemaining = 0; gullRemaining = -1; fishCooldown = 0; chat.Clear();
            stormRemaining = 0; stormStrikeCheck = 0; stormDelay = Random.Range(75f, 145f);
            audioSystem.Stop(); audioSystem.StartSea(); World.RefreshMarkers(State);
            Navigate(ScreenId.Playing); ui.SetFade(1);
        }
        public void EndVoyage()
        {
            ui.ClearNotice();
            atHelm = false; State = null; audioSystem.Stop(); fadeRemaining = 0; radioRemaining = 0; gullRemaining = -1;
            floodEventDelay = 0; floodEventRemaining = 0; floodDamageTimer = 0;
            floodEventScheduled = false; floodEventActive = false; stormEventCompleted = false;
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
                    audioSystem.Disconnect(); Navigate(ScreenId.Playing);
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
                    view.Tick(dt, atHelm, World);
                    float rudderInput = atHelm ? (Settings.Held(Control.Right) ? 1 : 0) - (Settings.Held(Control.Left) ? 1 : 0) : 0;
                    float throttleInput = atHelm ? (Settings.Held(Control.Forward) ? 1 : 0) - (Settings.Held(Control.Back) ? 1 : 0) : 0;
                    World.Steer(rudderInput, throttleInput, dt);
                    if (atHelm)
                    {
                        StoryStage beforeHeading = State.Stage;
                        State.SetHeading(World.Heading);
                        if (beforeHeading == StoryStage.SteerWest && State.Stage == StoryStage.GullStrike)
                        { gullRemaining = 2; World.StartGullStrike(); ui.Toast(T("서쪽 항로를 잡았습니다. 갈매기가 갑판으로 내려옵니다.", "Westward course set. A gull is diving toward the deck.")); }
                        ui.SetPrompt(T("조타 중  ·  속도 ", "At helm  ·  speed ") + World.SpeedKnots.ToString("0.0") + T(" 노트  ·  ", " kn  ·  ") + Settings.Binding(Control.Forward) + "/" + Settings.Binding(Control.Back) + T(" 전후진  ·  ", " throttle  ·  ") + Settings.Binding(Control.Left) + "/" + Settings.Binding(Control.Right) + T(" 조향  ·  ", " steer  ·  ") + Settings.Binding(Control.Interact) + T(" 내리기", " leave helm"));
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
                        for (int i = 0; i < State.HotbarSlots; i++)
                            if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) State.Select(i);
                    if (Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0)
                    {
                        int direction = Mouse.current.scroll.ReadValue().y > 0 ? -1 : 1;
                        State.Select((Mathf.Min(State.SelectedSlot, State.HotbarSlots - 1) + direction + State.HotbarSlots) % State.HotbarSlots);
                    }
                    State.Tick(dt);
                    State.TickBreath(dt, World.IsUnderwater(view.Camera.transform.position));
                    if (State.IsDead) Navigate(ScreenId.Dead);
                }
                fishCooldown = Mathf.Max(0, fishCooldown - dt);
                World.Tick(dt);
                if (gullRemaining >= 0)
                {
                    gullRemaining -= dt;
                    if (gullRemaining <= 0)
                    {
                        gullRemaining = -1;
                        State.BreakPurifier();
                        if (State.Stage == StoryStage.FindPurifierPart)
                        {
                            floodEventDelay = Random.Range(45f, 90f);
                            floodEventScheduled = true;
                        }
                        ui.Toast(T("갈매기가 정수기를 망가뜨렸습니다. 바다에 떨어진 부품을 찾아야 합니다.", "The gull damaged the purifier. Find the part that fell into the sea."));
                    }
                }
                TickStorm(dt);
                TickFloodEvent(dt);
                World.RefreshMarkers(State, floodEventActive);
                audioSystem.SetAlarm(State.Stage == StoryStage.Radio);
            }
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0) { refreshTimer = .2f; ui.RefreshHud(); }
        }
        private void TickFloodEvent(float dt)
        {
            if (State.Stage != StoryStage.FindPurifierPart
                && State.Stage != StoryStage.RepairPurifier
                && State.Stage != StoryStage.Complete) return;
            if (stormRemaining > 0) return;
            if (floodEventScheduled)
            {
                if (!stormEventCompleted) return;
                floodEventDelay -= dt;
                if (floodEventDelay > 0) return;
                floodEventScheduled = false;
                floodEventActive = true;
                floodEventRemaining = 180f;
                floodDamageTimer = 10f;
                World.SetDiveHatchOpen(true);
                ui.Toast(T("랜덤 이벤트: 잠수구 수밀문이 고장 났습니다. 3분 안에 닫으세요.", "Random event: The dive hatch failed. Close it within 3 minutes."));
            }
            if (!floodEventActive) return;

            floodEventRemaining -= dt;
            floodDamageTimer -= dt;
            if (floodDamageTimer <= 0)
            {
                floodDamageTimer = 10f;
                State.TakeDamage(2f);
                ui.Toast(T("물이 선내로 들어옵니다. 체력 -2 · 잠수구 수밀문을 닫으세요.", "Water is flooding the ship. Health -2 · Close the dive hatch."));
                if (State.IsDead)
                {
                    Navigate(ScreenId.Dead);
                    return;
                }
            }
            if (floodEventRemaining <= 0)
            {
                floodEventActive = false;
                World.SetDiveHatchOpen(false);
                State.TakeDamage(15f);
                ui.Toast(T("시간 안에 수밀문을 닫지 못했습니다. 침수로 체력 -15", "The hatch was not closed in time. Flooding caused 15 damage."));
                if (State.IsDead) Navigate(ScreenId.Dead);
            }
        }

        private void TickStorm(float dt)
        {
            // Random events start only after the gull has damaged the purifier (required event 4).
            if (State.Stage != StoryStage.FindPurifierPart
                && State.Stage != StoryStage.RepairPurifier
                && State.Stage != StoryStage.Complete) return;

            if (stormRemaining <= 0)
            {
                stormDelay -= dt;
                if (stormDelay > 0) return;
                stormRemaining = 180f; stormStrikeCheck = 10f;
                stormDelay = Random.Range(210f, 360f); World.SetStorm(true);
                ui.Toast(T("랜덤 이벤트: 천둥번개와 폭우가 시작됐습니다. 실내로 대피하세요.", "Random event: Thunderstorm. Take shelter indoors."));
                if (State.Stage == StoryStage.FindPurifierPart && State.SpawnPurifierPart())
                {
                    World.SpawnPurifierPartAtSea();
                    ui.Toast(T("폭풍 속에서 정수기 부품이 바다에 떠내려왔습니다. 잠수구로 나가 찾으세요.", "The storm washed a purifier part into the sea. Find it through the dive hatch."));
                }
                return;
            }
            stormRemaining -= dt; stormStrikeCheck -= dt;
            if (stormStrikeCheck <= 0)
            {
                stormStrikeCheck += 10f;
                if (Random.value < .4f && World.IsExposed(view.transform.position))
                {
                    World.FlashLightning(); State.TakeDamage(20);
                    ui.Toast(T("번개가 갑판을 쳤습니다! 체력 -20", "Lightning struck the exposed deck! Health -20"));
                    if (State.IsDead) Navigate(ScreenId.Dead);
                }
            }
            if (stormRemaining <= 0)
            {
                World.SetStorm(false); stormEventCompleted = true; State.RestoreThirst(); State.FillPurifierTank();
                ui.Toast(T("폭우가 그쳤습니다. 갈증과 정수기 물이 최대치로 회복되었습니다.", "The rain has passed. Thirst and purifier water are full."));
            }
        }
        public string StationName(DriftInteractable target)
        {
            switch (target.Kind)
            {
                case Station.Note: return T("오늘의 작업 읽기", "Read today's work");
                case Station.Fishing: return T("낚시하기", "Fish");
                case Station.Barrel: return T("빗물통에서 물 받기", "Collect rainwater");
                case Station.SeaWater: return T("양동이로 바닷물 뜨기", "Fill bucket with seawater");
                case Station.Purifier:
                    if (State == null) return T("정수기", "Purifier");
                    if (State.PurifierBroken) return T("정수기 수리", "Repair purifier");
                    if (State.PurificationRemaining > 0) return T("정수 중 " + Mathf.CeilToInt(State.PurificationRemaining) + "초", "Purifying " + Mathf.CeilToInt(State.PurificationRemaining) + "s");
                    return T("정수기 " + State.PurifierWater + "/" + DriftState.PurifierCapacity + " · 상호작용", "Purifier " + State.PurifierWater + "/" + DriftState.PurifierCapacity + " · interact");
                case Station.Engine: return T("엔진 연료 확인 / 보충", "Check / refill engine fuel");
                case Station.DeckRepair: return T("갑판 손상부 수리", "Repair deck");
                case Station.Radio: return T("낡은 라디오", "Old radio");
                case Station.Helm: return T("방향타 잡기", "Take helm");
                case Station.DeckHatch: return World.DeckHatchOpen ? T("갑판 해치 닫기", "Close deck hatch") : T("갑판 해치 열기", "Open deck hatch");
                case Station.LadderDeckMiddle: return T("중간층 사다리 이용", "Use ladder to middle deck");
                case Station.LadderMiddleLower: return T("아래층 사다리 이용", "Use ladder to lower deck");
                case Station.DiveHatch: return World.DiveHatchOpen ? T("잠수구 수밀문 닫기", "Close dive hatch") : T("잠수구 수밀문 열기", "Open dive hatch");
                default: return Settings.ItemName(target.PickupItem) + T(" 줍기", " · pick up");
            }
        }
        private void Interact(DriftInteractable target)
        {
            switch (target.Kind)
            {
                case Station.Note: State.ReadNote(); ui.Toast(T("오늘의 작업을 퀘스트에 등록했습니다.", "Today's work added to your objectives.")); break;
                case Station.Pickup:
                    if (State.TryAdd(target.PickupItem))
                    {
                        State.AcquirePurifierPart();
                        ui.Toast(Settings.ItemName(target.PickupItem) + T(" 획득", " collected"));
                        World.Collect(target);
                    }
                    else Full(); break;
                case Station.Fishing:
                    if (State.Count(Item.FishingRod) == 0) { ui.Toast(T("갑판의 낚싯대를 먼저 주우세요.", "Pick up the fishing rod on deck first.")); break; }
                    if (fishCooldown > 0) { ui.Toast(T("물고기가 다시 모이기를 기다리세요.", "Wait for the fish to return.")); break; }
                    if (State.TryAdd(Item.Fish)) { State.CompleteTask(TaskId.Food); fishCooldown = 4; ui.Toast(T("생선을 잡았습니다. 인벤토리에서 먹을 수 있습니다.", "Fish caught. Eat it from your inventory.")); } else Full(); break;
                case Station.Barrel:
                    if (State.TryAdd(Item.Water)) ui.Toast(T("물을 받았습니다.", "Water collected.")); else Full(); break;
                case Station.SeaWater:
                    if (State.TryAdd(Item.SeaWater)) ui.Toast(T("바닷물을 양동이에 담았습니다. 정수기에 넣으세요.", "Seawater collected. Bring it to the purifier.")); else Full(); break;
                case Station.Purifier:
                    if (State.PurifierBroken)
                    {
                        if (State.RepairPurifier())
                        {
                            bool minaUnlocked = Settings.UnlockMina();
                            ui.Toast(minaUnlocked
                                ? T("정수기를 수리했습니다. 미나가 해금되었습니다.", "Purifier repaired. Mina is unlocked.")
                                : T("정수기를 수리했습니다.", "Purifier repaired."));
                        }
                        else ui.Toast(T("정수기 부품이 필요합니다. 갑판의 흰색 상자를 찾으세요.", "A filter part is required. Find the white box on deck."));
                    }
                    else if (State.PurificationRemaining > 0)
                        ui.Toast(T("정수 중입니다. " + Mathf.CeilToInt(State.PurificationRemaining) + "초 후 완료됩니다.", "Purification is in progress. " + Mathf.CeilToInt(State.PurificationRemaining) + " seconds remaining."));
                    else if (State.Count(Item.SeaWater) > 0 && State.TryStartPurification())
                    {
                        State.CompleteTask(TaskId.Purifier);
                        ui.Toast(T("정수를 시작했습니다. 1분 후 식수가 만들어집니다.", "Purification started. Drinking water will be ready in one minute."));
                    }
                    else if (State.PurifierWater > 0)
                    {
                        if (State.TryCollectPurifiedWater()) ui.Toast(T("정수된 물을 받았습니다. 정수기 " + State.PurifierWater + "/" + DriftState.PurifierCapacity, "Purified water collected. Purifier " + State.PurifierWater + "/" + DriftState.PurifierCapacity));
                        else Full();
                    }
                    else if (State.PurifierWater >= DriftState.PurifierCapacity)
                        ui.Toast(T("정수기 저장 용량 30개가 가득 찼습니다.", "The purifier's 30-unit storage is full."));
                    else
                    {
                        State.CompleteTask(TaskId.Purifier);
                        ui.Toast(T("정수기는 정상입니다. 먼저 갑판 양동이로 바닷물을 떠오세요.", "The purifier is working. Fill the bucket with seawater first."));
                    }
                    break;
                case Station.Engine:
                    if (!WorkReady(TaskId.Fuel)) break;
                    if (State.TryRemove(Item.Fuel)) { State.CompleteTask(TaskId.Fuel); ui.Toast(T("연료를 보충하고 엔진 시동을 걸었습니다.", "Fuelled and started the engine.")); }
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
                case Station.DeckHatch:
                    World.SetDeckHatchOpen(!World.DeckHatchOpen);
                    ui.Toast(World.DeckHatchOpen ? T("갑판 해치를 열었습니다.", "Deck hatch opened.") : T("갑판 해치를 닫았습니다.", "Deck hatch closed."));
                    break;
                case Station.LadderDeckMiddle:
                    bool descendingFromDeck = view.transform.localPosition.y > -.5f;
                    if (!World.DeckHatchOpen && descendingFromDeck)
                    { ui.Toast(T("갑판 해치를 먼저 열어야 내려갈 수 있습니다.", "Open the deck hatch before climbing down.")); break; }
                    if (!descendingFromDeck && !World.DeckHatchOpen)
                    {
                        // A closed hatch must never trap a player in the lower decks.
                        World.SetDeckHatchOpen(true);
                        ui.Toast(T("아래쪽 해치를 열어 탈출 경로를 확보했습니다.", "The hatch opened from below to keep the escape route clear."));
                    }
                    if (World.TryGetLadderDestination(target.Kind, view.transform.localPosition.y, out Vector3 middlePosition))
                        view.MoveTo(middlePosition);
                    break;
                case Station.LadderMiddleLower:
                    if (World.TryGetLadderDestination(target.Kind, view.transform.localPosition.y, out Vector3 lowerPosition))
                        view.MoveTo(lowerPosition);
                    break;
                case Station.DiveHatch:
                    Vector3 hatchPlayerPosition = view.transform.localPosition;
                    bool closingFromSea = World.DiveHatchOpen && hatchPlayerPosition.y < -2.8f && hatchPlayerPosition.x > 4.2f && hatchPlayerPosition.z > 6.6f && hatchPlayerPosition.z < 10.1f;
                    if (closingFromSea)
                    { ui.Toast(T("잠수구 밖에서는 문을 잠글 수 없습니다. 먼저 선내로 들어오세요.", "Return inside before closing the dive door.")); break; }
                    bool wasOpen = World.DiveHatchOpen;
                    World.SetDiveHatchOpen(!wasOpen);
                    if (floodEventActive && wasOpen)
                    {
                        floodEventActive = false; floodEventRemaining = 0;
                        State.RestoreThirst();
                        bool waterAdded = State.TryAdd(Item.Water);
                        if (!waterAdded) World.AddPickup(Item.Water, new Vector3(-3.1f, 1.2f, 8.5f), true);
                        ui.Toast(waterAdded
                            ? T("수밀문을 닫아 침수를 막았습니다. 갈증이 회복되고 식수 1개를 얻었습니다.", "Hatch secured. Thirst restored and one drinking water received.")
                            : T("수밀문을 닫아 침수를 막았습니다. 갈증이 회복되었습니다. 식수는 갑판에 놓였습니다.", "Hatch secured. Thirst restored. Drinking water was placed on deck."));
                    }
                    else ui.Toast(World.DiveHatchOpen ? T("잠수구 수밀문을 열었습니다.", "Dive hatch opened.") : T("잠수구 수밀문을 닫았습니다.", "Dive hatch closed."));
                    break;
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
            local.x = Mathf.Clamp(local.x, -5.3f, 5.3f); local.z = Mathf.Clamp(local.z, -14, 14);
            Vector3 playerShipPosition = World.Ship.InverseTransformPoint(view.transform.position);
            local.y = World.InsideWheelhouse(view.transform.position) ? 3.2f
                : playerShipPosition.y > -.5f ? 1.2f
                : playerShipPosition.y > -3.5f ? -1.92f : -4.92f;
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
                    case StoryStage.SteerWest: quest.Append(T("나침반을 보며 키를 서쪽 270°로 돌리세요.", "Use the compass to steer west, 270°.")); break;
                    case StoryStage.GullStrike: quest.Append(T("갈매기가 갑판으로 내려오고 있습니다.", "A gull is diving toward the deck.")); break;
                    case StoryStage.FindPurifierPart: quest.Append(T("폭풍 이벤트를 기다린 뒤 잠수구를 통해 바다에서 정수기 부품을 찾으세요.", "Wait for a storm event, then search the sea for the purifier part through the dive hatch.")); break;
                    case StoryStage.RepairPurifier: quest.Append(T("정수기 부품을 조타실 아래층의 정수기에 가져가 수리하세요.", "Bring the purifier part to the purifier below the wheelhouse and repair it.")); break;
                    case StoryStage.Complete: quest.Append(T("모든 작업을 완료했습니다.\n서쪽을 향한 항해가 시작됩니다.", "All tasks complete.\nYour voyage west begins.")); break;
                }
                if (stormRemaining > 0)
                { quest.AppendLine(); quest.Append(T("랜덤 이벤트: 폭풍우 · 실내에서 지나가길 기다리세요.", "Random event: Storm · wait it out indoors.")); }
                if (floodEventActive)
                {
                    quest.AppendLine();
                    quest.Append(T("비상 이벤트: 3분 안에 잠수구 수밀문 닫기 · 남은 시간 ", "Emergency event: close the dive hatch within 3 minutes · time left "));
                    quest.Append(Mathf.CeilToInt(floodEventRemaining)); quest.Append(T("초", " sec"));
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
