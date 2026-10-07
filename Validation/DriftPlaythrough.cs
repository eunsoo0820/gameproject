using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Drift;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only external validation. Teleports between stations, then exercises
// real raycasts, keyboard events, normal Update calls and elapsed game frames.
public static class DriftPlaythrough
{
    private static DriftApplication app;
    private static readonly List<string> checks = new List<string>();
    private static bool previousBackground;
    private static InputSettings previousInputSettings;
    private static InputSettings testInputSettings;
    private static Keyboard testKeyboard;
    private static string reportPath;
    public static string Run()
    {
        app = UnityEngine.Object.FindAnyObjectByType<DriftApplication>();
        if (!Application.isPlaying || app == null) throw new Exception("Play Mode required.");
        checks.Clear();
        reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/playthrough-result.txt"));
        File.WriteAllText(reportPath, "RUNNING");
        previousBackground = Application.runInBackground;
        // Input System can destroy its generated default when settings are replaced.
        // Preserve a separate snapshot so cleanup never restores a destroyed object.
        previousInputSettings = UnityEngine.Object.Instantiate(InputSystem.settings);
        previousInputSettings.hideFlags = HideFlags.DontSave;
        testInputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = testInputSettings;
        testKeyboard = InputSystem.AddDevice<Keyboard>("Drift Validation Keyboard");
        testKeyboard.MakeCurrent();
        Application.runInBackground = true;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorApplication).Assembly.GetType("UnityEditor.GameView")).Focus();
        app.StartCoroutine(Guard(Sequence()));
        return "STARTED: bounded Play Mode tutorial validation";
    }
    private static IEnumerator Guard(IEnumerator sequence)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 90;
        string failure = null;
        while (true)
        {
            bool more = false;
            try
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new Exception("Playthrough timed out");
                if (Keyboard.current != testKeyboard) testKeyboard.MakeCurrent();
                if (app.Screen == ScreenId.Pause) app.Navigate(ScreenId.Playing);
                more = sequence.MoveNext();
            }
            catch (Exception error) { failure = error.ToString(); }
            if (!more || failure != null) break;
            yield return sequence.Current;
        }
        try
        {
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            InputSystem.RemoveDevice(testKeyboard);
            Application.runInBackground = previousBackground;
            InputSystem.settings = previousInputSettings;
            UnityEngine.Object.Destroy(testInputSettings);
        }
        catch (Exception error) { failure = (failure ?? "") + "\nCleanup: " + error; }
        finally { File.WriteAllText(reportPath, string.Join("\n", checks) + "\n" + (failure ?? "ALL PASSED")); }
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name + "; screen=" + app.Screen + "; stage=" + app.State.Stage);
        checks.Add("PASS: " + name);
    }
    private static IEnumerator Tap(Control control)
    {
        testKeyboard.MakeCurrent();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(app.Settings.Binding(control)));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        yield return null;
        yield return null;
    }
    private static IEnumerator Sequence()
    {
        app.StartVoyage();
        var fade = typeof(DriftApplication).GetField("fadeRemaining", BindingFlags.NonPublic | BindingFlags.Instance);
        while ((float)fade.GetValue(app) > 0) yield return null;
        double ready;
        Aim(Station.Note);
        var tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.Stage == StoryStage.DailyWork, "F reads actual wall note");
        Aim(Station.Pickup, Item.FishingRod);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.Count(Item.FishingRod) == 1, "F picks up fishing rod");
        Aim(Station.Fishing);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.IsDone(TaskId.Food), "Fishing completes food objective");
        Aim(Station.Purifier);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.IsDone(TaskId.Purifier), "Purifier inspection");
        Aim(Station.Pickup, Item.Fuel);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Aim(Station.Engine);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.IsDone(TaskId.Fuel) && app.State.Count(Item.Fuel) == 0, "Refuel consumes fuel");
        Aim(Station.Pickup, Item.Scrap);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Aim(Station.DeckRepair);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.Stage == StoryStage.Radio && app.State.Count(Item.Scrap) == 0, "Four tasks trigger radio alarm");
        Aim(Station.Radio);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.Screen == ScreenId.Dialogue, "Radio opens warning dialogue");
        ready = Time.timeAsDouble + 8;
        while (!app.State.PurifierBroken && Time.timeAsDouble < ready) yield return null;
        Check(app.State.PurifierBroken && app.Screen == ScreenId.Playing, "Radio ends and gull breaks purifier");
        Aim(Station.Pickup, Item.PurifierPart);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Aim(Station.Purifier);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        Check(app.State.PurifierRepaired && app.State.Count(Item.PurifierPart) == 0, "Repair consumes filter part");
        Aim(Station.Helm);
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(app.Settings.Binding(Control.Left)));
        ready = Time.timeAsDouble + 7;
        while (!app.State.WestReached && Time.timeAsDouble < ready) yield return null;
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        yield return null;
        Check(app.State.WestReached, "Held left key steers actual ship west");
        Check(app.State.Stage == StoryStage.Complete, "Entire tutorial completes");
        tap = Tap(Control.Interact); while (tap.MoveNext()) yield return tap.Current;
    }
    private static void Aim(Station kind, Item item = Item.None)
    {
        var view = UnityEngine.Object.FindAnyObjectByType<DriftView>();
        DriftInteractable target = null;
        foreach (var candidate in app.World.Stations)
            if (candidate.gameObject.activeSelf && candidate.Kind == kind && (kind != Station.Pickup || candidate.PickupItem == item)) { target = candidate; break; }
        if (target == null) throw new Exception("Missing station " + kind + "/" + item);
        Vector3 local = target.transform.localPosition;
        Vector3 position = local; position.y = 1.03f;
        if (kind == Station.Radio || kind == Station.Helm || kind == Station.Fishing || item == Item.FishingRod)
            position.z += kind == Station.Fishing || item == Item.FishingRod ? 1.8f : -1.8f;
        else position.x += local.x > 0 ? -1.8f : 1.8f;
        var controller = view.GetComponent<CharacterController>();
        controller.enabled = false; view.transform.localPosition = position;
        Vector3 direction = local - (position + Vector3.up * 1.6f);
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
        view.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        typeof(DriftView).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, pitch);
        view.Camera.transform.localPosition = Vector3.up * 1.6f;
        view.Camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        controller.enabled = true; Physics.SyncTransforms(); app.Navigate(ScreenId.Playing);
        if (view.Target() != target) throw new Exception("Raycast obstructed at " + kind + "/" + item);
    }
}
