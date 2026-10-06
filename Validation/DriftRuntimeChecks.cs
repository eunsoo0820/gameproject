using System;
using System.Collections.Generic;
using Drift;
using UnityEngine;
using UnityEngine.InputSystem;

public static class DriftRuntimeChecks
{
    private static readonly List<string> results = new List<string>();
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        results.Add("PASS: " + name);
    }
    private static UnityEngine.UI.Button FindButton(string name)
    {
        foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>())
            if (button.gameObject.activeInHierarchy && button.name == name) return button;
        throw new Exception("Missing button: " + name);
    }
    public static string Run()
    {
        results.Clear();
        var app = UnityEngine.Object.FindAnyObjectByType<DriftApplication>();
        if (!Application.isPlaying || app == null) throw new InvalidOperationException("Drift must be running.");
        var settings = app.Settings;
        bool originalLanguage = settings.Korean; int originalVolume = settings.Volume; bool originalChat = settings.ChatEnabled;
        try
        {
            app.EndVoyage();
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length == 1, "Exactly one EventSystem");
            Check(UnityEngine.Object.FindAnyObjectByType<Canvas>().GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Canvas raycaster exists");
            FindButton(settings.Text("항해", "Voyage")).onClick.Invoke(); Check(app.Screen == ScreenId.Voyage, "Voyage button");
            Check(!FindButton(settings.Text("방 들어가기 · 추후 지원", "Join room · coming later")).interactable, "Join room explicitly disabled");
            FindButton(settings.Text("방 만들기", "Create room")).onClick.Invoke(); Check(app.Screen == ScreenId.Room, "Local room creation");
            FindButton(settings.Text("항해 시작", "Begin voyage")).onClick.Invoke(); Check(app.State != null && app.Screen == ScreenId.Playing, "Start button creates playable session");
            Check(UnityEngine.Object.FindAnyObjectByType<DriftView>().GetComponent<CharacterController>() != null, "Invisible test controller exists");
            app.Navigate(ScreenId.Pause); app.OpenSettings();
            FindButton(settings.Text("언어 · 한국어", "Language · English")).onClick.Invoke(); Check(settings.Korean != originalLanguage, "Language UI changes language");
            FindButton(settings.Text("언어 · 한국어", "Language · English")).onClick.Invoke();
            var slider = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UI.Slider>();
            slider.value = 0; Check(settings.Volume == 0 && AudioListener.volume == 0, "Volume 0 mutes audio");
            slider.value = 100; Check(settings.Volume == 100 && AudioListener.volume == 1, "Volume 100 restores gain"); slider.value = originalVolume;
            Check(!settings.Rebind(Control.Interact, settings.Binding(Control.Forward)), "Conflicting key rejected");
            Check(!settings.Rebind(Control.Interact, Key.Escape), "Reserved UI key rejected");
            FindButton(settings.Text("채팅", "Chat") + "  " + (settings.ChatEnabled ? "ON" : "OFF")).onClick.Invoke();
            Check(settings.ChatEnabled != originalChat, "Chat option toggles"); settings.SetChat(true);
            app.SendLocalChat("<b>test</b>"); Check(app.ChatHistory.Contains("‹b›test‹/b›"), "Chat text cannot inject TMP markup");
            for (int i = 0; i < 20; i++) app.SendLocalChat("message " + i);
            Check(app.ChatHistory.Split('\n').Length == 8, "Chat history is bounded");
            app.State.TryAdd(Item.Fish, 2); app.State.Select(0); app.Navigate(ScreenId.Inventory);
            FindButton(settings.Text("선택 아이템 사용", "Use selected")).onClick.Invoke(); Check(app.State.Count(Item.Fish) == 1, "Inventory use button consumes one");
            FindButton(settings.Text("1개 버리기", "Drop one")).onClick.Invoke(); Check(app.State.Count(Item.Fish) == 0, "Inventory drop button removes one");
            int dropped = 0;
            foreach (var pickup in UnityEngine.Object.FindObjectsByType<DriftInteractable>()) if (pickup.Dropped) dropped++;
            Check(dropped == 1, "Dropping creates recoverable world pickup");
            app.EndVoyage(); app.StartVoyage(); Check(app.State.Count(Item.Fish) == 0 && app.State.Stage == StoryStage.FindNote, "Restart resets session");
            return string.Join("\n", results);
        }
        finally
        {
            settings.SetLanguage(originalLanguage); settings.SetVolume(originalVolume); settings.SetChat(originalChat); settings.Save(); app.EndVoyage();
        }
    }
}

