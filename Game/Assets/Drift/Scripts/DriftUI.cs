using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Drift
{
    public enum ScreenId { Menu, Voyage, Room, CharacterSelect, Settings, Credits, Playing, Pause, Inventory, Chat, Dialogue, Dead }
    public sealed class DriftUI
    {
        private readonly DriftApplication app;
        private readonly DriftSettings settings;
        private readonly TMP_FontAsset font;
        private readonly RectTransform canvas, pages, hud;
        private readonly List<UnityEngine.UI.Button> pageButtons = new List<UnityEngine.UI.Button>();
        private readonly TMP_Text objectives, vitals, prompt, heading, notice;
        private readonly TMP_Text[] hotbar = new TMP_Text[DriftState.HotbarCount];
        private readonly UnityEngine.UI.Image[] hotbarPanels = new UnityEngine.UI.Image[DriftState.HotbarCount];
        private readonly UnityEngine.UI.Image fade;
        private TMP_Text bindingStatus, volumeLabel;
        private TMP_InputField chatInput;
        private UnityEngine.UI.Slider volumeSlider;
        private float noticeUntil;
        private readonly Color ink = new Color(.035f, .075f, .09f, .96f);
        private readonly Color paper = new Color(.91f, .88f, .79f);
        private readonly Color teal = new Color(.19f, .51f, .49f);

        public DriftUI(DriftApplication owner, TMP_FontAsset typeface)
        {
            app = owner; settings = app.Settings; font = typeface;
            var go = new GameObject("Drift UI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(owner.transform, false); canvas = (RectTransform)go.transform;
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var events = new GameObject("Drift EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(owner.transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            hud = Rect("HUD", canvas, 0, 0, 1920, 1080); Stretch(hud);
            var questBack = Panel(hud, 38, 38, 510, 330, new Color(.025f, .06f, .07f, .75f));
            objectives = Label(questBack, "", 22, 20, 470, 290, 23);
            vitals = Label(hud, "", 1510, 880, 355, 150, 25);
            Anchor(vitals.rectTransform, new Vector2(1, 0), new Vector2(-38, 35));
            heading = Label(hud, "", 690, 28, 540, 52, 24); heading.alignment = TextAlignmentOptions.Center;
            Anchor(heading.rectTransform, new Vector2(.5f, 1), new Vector2(0, -28));
            prompt = Label(hud, "", 555, 735, 810, 105, 25); prompt.alignment = TextAlignmentOptions.Center;
            Anchor(prompt.rectTransform, new Vector2(.5f, 0), new Vector2(0, 180));
            var crosshair = Label(hud, "·", 943, 510, 34, 42, 34); crosshair.alignment = TextAlignmentOptions.Center;
            Anchor(crosshair.rectTransform, new Vector2(.5f, .5f), Vector2.zero);
            var hotbarRoot = Rect("Hotbar", hud, 0, 0, 1000, 87);
            Anchor(hotbarRoot, new Vector2(.5f, 0), new Vector2(0, 30));
            for (int i = 0; i < hotbar.Length; i++)
            {
                RectTransform slot = Panel(hotbarRoot, i * 125, 0, 118, 87, ink);
                hotbarPanels[i] = slot.GetComponent<UnityEngine.UI.Image>();
                hotbar[i] = Label(slot, "", 8, 8, 104, 72, 17); hotbar[i].alignment = TextAlignmentOptions.Center;
            }
            pages = Rect("Pages", canvas, 0, 0, 1920, 1080); Stretch(pages);
            notice = Label(canvas, "", 500, 840, 920, 70, 25); notice.alignment = TextAlignmentOptions.Center;
            Anchor(notice.rectTransform, new Vector2(.5f, 0), new Vector2(0, 110));
            fade = Panel(canvas, 0, 0, 1920, 1080, Color.black).GetComponent<UnityEngine.UI.Image>(); Stretch(fade.rectTransform);
            fade.raycastTarget = false; fade.gameObject.SetActive(false);
        }
        private string T(string ko, string en) => settings.Text(ko, en);
        private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Anchor(RectTransform rect, Vector2 point, Vector2 position)
        { rect.anchorMin = rect.anchorMax = rect.pivot = point; rect.anchoredPosition = position; }
        private RectTransform Panel(Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect("Panel", parent, x, y, width, height); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return rect;
        }
        private TMP_Text Label(Transform parent, string text, float x, float y, float width, float height, float size)
        {
            var rect = Rect("Label", parent, x, y, width, height); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = size; label.color = paper; label.text = text; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        private UnityEngine.UI.Button Button(Transform parent, string text, float x, float y, float width, float height, Action action, bool enabled = true)
        {
            var rect = Panel(parent, x, y, width, height, enabled ? new Color(.13f, .24f, .26f, .98f) : new Color(.10f, .14f, .15f, .7f));
            rect.name = text; var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.interactable = enabled;
            var colors = button.colors; colors.highlightedColor = new Color(.6f, .9f, .85f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            var textLabel = Label(rect, text, 20, 5, width - 40, height - 10, 25); textLabel.alignment = TextAlignmentOptions.MidlineLeft;
            if (!enabled) textLabel.color = new Color(.5f, .57f, .57f);
            if (action != null) button.onClick.AddListener(() => action()); pageButtons.Add(button); return button;
        }
        private RectTransform Page(string title, string subtitle)
        {
            var panel = Panel(pages, 70, 55, 1780, 970, ink);
            Anchor(panel, new Vector2(.5f, .5f), Vector2.zero);
            Panel(panel, 45, 47, 5, 62, teal);
            Label(panel, title, 70, 34, 1450, 78, 50);
            Label(panel, subtitle, 72, 117, 1630, 70, 23);
            return panel;
        }
        public void Show(ScreenId screen)
        {
            foreach (var button in pageButtons) if (button != null) button.onClick.RemoveAllListeners(); pageButtons.Clear();
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveAllListeners();
            if (chatInput != null) chatInput.onSubmit.RemoveAllListeners();
            for (int i = pages.childCount - 1; i >= 0; i--) { var old = pages.GetChild(i).gameObject; old.SetActive(false); UnityEngine.Object.Destroy(old); }
            bindingStatus = null; chatInput = null; volumeSlider = null; volumeLabel = null;
            hud.gameObject.SetActive(screen == ScreenId.Playing);
            if (screen == ScreenId.Playing) return;
            switch (screen)
            {
                case ScreenId.Menu: MainMenu(); break;
                case ScreenId.Voyage:
                    var voyage = Page(T("항해", "VOYAGE"), T("새로운 항해를 준비하세요.", "Prepare your next voyage."));
                    Button(voyage, T("방 만들기", "Create room"), 72, 245, 650, 82, app.CreateRoom);
                    Button(voyage, T("방 들어가기 · 추후 지원", "Join room · coming later"), 72, 350, 650, 82, null, false);
                    Back(voyage, () => app.Navigate(ScreenId.Menu)); break;
                case ScreenId.Room:
                    var room = Page(T("출항 준비", "READY TO DEPART"), T("로컬 방 · 참가자 1명\n온라인 접속은 이후 버전에서 지원합니다.", "Local room · 1 participant\nOnline connections arrive in a later version."));
                    Label(room, T("낡은 선박, 끊어진 무전.\n오늘의 작업부터 시작하세요.", "An aging ship. A broken transmission.\nBegin with today's work."), 72, 245, 1100, 145, 34);
                    Button(room, T("캐릭터 선택", "Choose character"), 72, 450, 650, 82, () => app.Navigate(ScreenId.CharacterSelect)); Back(room, () => app.Navigate(ScreenId.Voyage)); break;
                case ScreenId.CharacterSelect: CharacterSelectPage(); break;
                case ScreenId.Settings: SettingsPage(); break;
                case ScreenId.Credits:
                    var credits = Page(T("크레딧", "CREDITS"), "drift");
                    Label(credits, T("제작진 정보는 추후 등록됩니다.\n\n현재 오디오는 절차적으로 합성한 임시 효과음입니다.", "Production credits will be added later.\n\nCurrent audio uses procedural placeholder effects."), 72, 245, 1500, 250, 29);
                    Back(credits, () => app.Navigate(ScreenId.Menu)); break;
                case ScreenId.Pause:
                    var pause = Page(T("잠시 정박", "PAUSED"), T("로컬 항해가 일시 정지되었습니다.", "Your local voyage is paused."));
                    Button(pause, T("계속하기", "Resume"), 72, 240, 650, 82, () => app.Navigate(ScreenId.Playing));
                    Button(pause, T("설정", "Settings"), 72, 345, 650, 82, app.OpenSettings);
                    Button(pause, T("메뉴로 · 현재 항해 종료", "Main menu · end voyage"), 72, 450, 650, 82, app.EndVoyage); break;
                case ScreenId.Inventory: InventoryPage(); break;
                case ScreenId.Chat: ChatPage(); break;
                case ScreenId.Dialogue:
                    var dialog = Page(T("끊어진 무전", "BROKEN TRANSMISSION"), T("낡은 라디오", "Old radio"));
                    Label(dialog, T("“서쪽에… 치직… 땅이… 치직…”", "“To the west… krrsh… land… krrsh…”"), 72, 300, 1560, 180, 46); break;
                case ScreenId.Dead:
                    var dead = Page(T("항해 중단", "VOYAGE ENDED"), T("체력이 모두 소진되었습니다.", "Your health has run out."));
                    Button(dead, T("새 항해", "New voyage"), 72, 270, 650, 82, app.StartVoyage); Back(dead, app.EndVoyage); break;
            }
            if (screen != ScreenId.Chat)
                foreach (var button in pageButtons) if (button.interactable) { EventSystem.current.SetSelectedGameObject(button.gameObject); break; }
        }
        private void MainMenu()
        {
            var panel = Panel(pages, 0, 0, 810, 1080, new Color(.025f, .06f, .075f, .93f));
            Panel(panel, 104, 145, 80, 4, teal);
            Label(panel, "A SURVIVAL VOYAGE", 104, 173, 650, 44, 22);
            Label(panel, "drift", 91, 210, 680, 225, 150);
            Label(panel, T("수평선 너머, 아직 끝나지 않은 항해.", "Beyond the horizon, the voyage continues."), 104, 441, 620, 90, 27);
            Button(panel, T("항해", "Voyage"), 104, 581, 590, 85, () => app.Navigate(ScreenId.Voyage));
            Button(panel, T("설정", "Settings"), 104, 681, 590, 85, app.OpenSettings);
            Button(panel, T("크레딧", "Credits"), 104, 781, 590, 85, () => app.Navigate(ScreenId.Credits));
            Label(panel, "DRIFT  /  EARLY DEVELOPMENT", 104, 972, 640, 38, 18);
            Label(pages, T("바다는 모든 것을 기억한다.", "The sea remembers."), 1140, 917, 670, 75, 33);
        }
        private void CharacterSelectPage()
        {
            var panel = Page(T("선원 선택", "CHOOSE YOUR CREW"), T("캐릭터 외형은 임시로 비워두고, 명세서의 능력치만 적용합니다.", "Character art is a placeholder; listed stats come from the requirements."));
            for (int i = 0; i < CharacterProfile.All.Length; i++)
            {
                var profile = CharacterProfile.All[i];
                float x = 72 + (i % 4) * 420, y = 225 + (i / 4) * 250;
                var button = Button(panel,
                    profile.Name + "\n" + T("갈증 ", "Thirst ") + profile.Thirst + "   " + T("허기 ", "Hunger ") + profile.Hunger + "   " + T("체력 ", "Health ") + profile.Health +
                    "\n" + T("가방 칸 ", "Bag ") + profile.Inventory + "   " + T("기술 ", "Skill ") + profile.Skill + "   " + T("숨 ", "Breath ") + profile.Breath + "   " + T("속도 ", "Speed ") + profile.Speed,
                    x, y, 390, 210, () => { app.SelectCharacter(profile.Id); Show(ScreenId.CharacterSelect); });
                var label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 21; label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                if (app.SelectedCharacter == profile.Id) button.targetGraphic.color = teal;
            }
            Label(panel, T("능력치 선택 화면은 플레이 테스트용입니다. 캐릭터 그림과 UI는 나중에 교체할 수 있습니다.", "This is a playable selection prototype. Character art and UI can be replaced later."), 72, 760, 1500, 52, 21);
            Button(panel, T("선택한 선원으로 출항", "Depart with selected character"), 1050, 835, 610, 75, app.StartVoyage);
            Back(panel, () => app.Navigate(ScreenId.Room));
        }
        private void Back(Transform parent, Action action) => Button(parent, T("뒤로", "Back"), 72, 835, 330, 70, action);
        private void SettingsPage()
        {
            var panel = Page(T("설정", "SETTINGS"), T("변경 사항은 자동 저장됩니다.", "Changes are saved automatically."));
            Button(panel, T("언어 · 한국어", "Language · English"), 72, 220, 660, 65, () => { settings.SetLanguage(!settings.Korean); Show(ScreenId.Settings); });
            volumeLabel = Label(panel, T("전체 소리", "Master volume") + "  " + settings.Volume, 72, 315, 650, 45, 26);
            var track = Panel(panel, 72, 375, 650, 25, new Color(.12f, .2f, .23f)); track.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            volumeSlider = track.gameObject.AddComponent<UnityEngine.UI.Slider>(); volumeSlider.minValue = 0; volumeSlider.maxValue = 100; volumeSlider.wholeNumbers = true;
            var fill = Panel(track, 0, 0, 650, 25, teal); Stretch(fill); volumeSlider.fillRect = fill;
            var handle = Panel(track, 0, 0, 23, 39, paper); Anchor(handle, new Vector2(0, .5f), Vector2.zero);
            handle.pivot = new Vector2(.5f, .5f); volumeSlider.handleRect = handle; volumeSlider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            volumeSlider.SetValueWithoutNotify(settings.Volume);
            volumeSlider.onValueChanged.AddListener(value => { settings.SetVolume(value); volumeLabel.text = T("전체 소리", "Master volume") + "  " + settings.Volume; });
            Button(panel, T("보이스", "Voice") + "  " + (settings.VoiceEnabled ? "ON" : "OFF"), 72, 465, 650, 65, () => { settings.SetVoice(!settings.VoiceEnabled); Show(ScreenId.Settings); });
            Button(panel, T("채팅", "Chat") + "  " + (settings.ChatEnabled ? "ON" : "OFF"), 72, 545, 650, 65, () => { settings.SetChat(!settings.ChatEnabled); Show(ScreenId.Settings); });
            Label(panel, T("보이스·채팅은 설정 및 화면만 지원합니다.\n실제 플레이어 간 통신은 아직 연결되지 않았습니다.", "Voice and chat settings / UI only.\nCommunication between players is not connected yet."), 72, 642, 670, 120, 22);
            for (int i = 0; i < 10; i++)
            {
                var control = (Control)i;
                Button(panel, settings.ControlName(control) + "   [" + settings.Binding(control) + "]", 890, 209 + i * 55, 785, 48, () => app.BeginRebind(control));
            }
            bindingStatus = Label(panel, T("변경할 동작을 선택한 뒤 키를 누르세요. ESC: 취소", "Select an action, then press a key. ESC: cancel"), 890, 778, 790, 52, 20);
            Button(panel, T("키 설정 초기화", "Reset bindings"), 890, 845, 620, 64, () => { settings.ResetBindings(); Show(ScreenId.Settings); });
            Back(panel, app.CloseSettings);
        }
        public void RebindMessage(string message) { if (bindingStatus != null) bindingStatus.text = message; }
        private void InventoryPage()
        {
            var panel = Page(T("인벤토리", "INVENTORY"), T("첫 8칸은 핫바입니다. 슬롯 선택 후 사용하거나 버릴 수 있습니다.", "The first 8 slots are your hotbar. Select a slot to use or drop its item."));
            for (int i = 0; i < DriftState.SlotCount; i++)
            {
                int slot = i; var stack = app.State.GetSlot(i);
                Button(panel, (i + 1) + "\n" + settings.ItemName(stack.Item) + (stack.Count > 0 ? " ×" + stack.Count : ""), 72 + i % 8 * 205, 235 + i / 8 * 150, 190, 130,
                    () => { app.State.Select(slot); Show(ScreenId.Inventory); }, i < app.State.InventorySlots);
                if (i == app.State.SelectedSlot) pageButtons[pageButtons.Count - 1].targetGraphic.color = teal;
            }
            Button(panel, T("선택 아이템 사용", "Use selected"), 72, 720, 490, 70, () => { app.UseItem(); Show(ScreenId.Inventory); });
            Button(panel, T("1개 버리기", "Drop one"), 590, 720, 490, 70, () => { app.DropItem(); Show(ScreenId.Inventory); });
            Back(panel, () => app.Navigate(ScreenId.Playing));
        }
        private void ChatPage()
        {
            var panel = Page(T("채팅", "CHAT"), T("로컬 미리보기 · 다른 플레이어에게 전송되지 않습니다.", "Local preview · messages are not sent to other players."));
            Label(panel, app.ChatHistory, 72, 220, 1580, 445, 27);
            var field = Panel(panel, 72, 690, 1580, 75, new Color(.12f, .22f, .24f)); field.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            chatInput = field.gameObject.AddComponent<TMP_InputField>(); var text = (TextMeshProUGUI)Label(field, "", 18, 12, 1540, 52, 25);
            chatInput.textViewport = field; chatInput.textComponent = text; chatInput.characterLimit = 160;
            chatInput.onSubmit.AddListener(message => { if (!string.IsNullOrWhiteSpace(message)) app.SendLocalChat(message); });
            Back(panel, () => app.Navigate(ScreenId.Playing)); chatInput.ActivateInputField();
        }
        public void Toast(string text) { notice.text = text; noticeUntil = Time.unscaledTime + 4; }
        public void ClearNotice() { notice.text = ""; noticeUntil = 0; }
        public void SetFade(float alpha) { fade.gameObject.SetActive(alpha > 0); fade.color = new Color(0, 0, 0, alpha); }
        public void RefreshHud()
        {
            if (Time.unscaledTime > noticeUntil) notice.text = "";
            if (app.State == null) return;
            var state = app.State;
            objectives.text = app.QuestText;
            vitals.text = T("갈증", "Thirst") + "  " + Mathf.CeilToInt(state.Thirst / state.ThirstMax * 100) + "%\n" + T("배고픔", "Hunger") + "  " + Mathf.CeilToInt(state.Hunger / state.HungerMax * 100) + "%\n" + T("체력", "Health") + "  " + Mathf.CeilToInt(state.Health / state.HealthMax * 100) + "%";
            if (state.Breath < state.BreathMax) vitals.text += "\n" + T("숨", "Breath") + "  " + Mathf.CeilToInt(state.Breath) + "s";
            heading.text = T("나침반", "COMPASS") + "  " + Mathf.RoundToInt(app.World.Heading).ToString("000") + "°   N 000 · E 090 · S 180 · W 270";
            for (int i = 0; i < hotbar.Length; i++)
            {
                var stack = state.GetSlot(i); bool available = i < state.InventorySlots;
                hotbar[i].text = available ? (i + 1) + "\n" + settings.ItemName(stack.Item) + (stack.Count > 0 ? " ×" + stack.Count : "") : "—";
                hotbarPanels[i].color = !available ? new Color(.06f, .08f, .09f, .7f) : state.SelectedSlot == i ? teal : ink;
            }
        }
        public void SetPrompt(string text) { if (prompt.text != text) prompt.text = text; }
        public void Dispose()
        {
            foreach (var button in pageButtons) if (button != null) button.onClick.RemoveAllListeners();
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveAllListeners();
            if (chatInput != null) chatInput.onSubmit.RemoveAllListeners();
            pageButtons.Clear();
        }
    }
}
