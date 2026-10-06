using System;
using System.Reflection;
using Drift;
using UnityEngine;

// Test setup only: puts the invisible viewpoint near an actual scene collider.
// Interactions themselves are subsequently driven with real Input System key events.
public static class DriftInteractionHarness
{
    public static string Aim(string kind, string item = "None")
    {
        var app = UnityEngine.Object.FindAnyObjectByType<DriftApplication>();
        var view = UnityEngine.Object.FindAnyObjectByType<DriftView>();
        Station stationKind = (Station)Enum.Parse(typeof(Station), kind);
        Item pickup = (Item)Enum.Parse(typeof(Item), item);
        DriftInteractable station = null;
        foreach (var candidate in app.World.Stations)
            if (candidate.gameObject.activeSelf && candidate.Kind == stationKind && (stationKind != Station.Pickup || candidate.PickupItem == pickup)) { station = candidate; break; }
        if (station == null) throw new Exception("Station not found: " + kind + " / " + item);
        Vector3 local = station.transform.localPosition;
        Vector3 position = local;
        position.y = 1.03f;
        if (stationKind == Station.Radio || stationKind == Station.Helm || stationKind == Station.Fishing || pickup == Item.FishingRod)
            position.z += stationKind == Station.Fishing || pickup == Item.FishingRod ? 1.8f : -1.8f;
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
        controller.enabled = true; Physics.SyncTransforms();
        app.Navigate(ScreenId.Playing);
        if (view.Target() != station) throw new Exception("Station blocked from test approach: " + kind);
        return "READY " + kind + " " + item + " interact=" + app.Settings.Binding(Control.Interact);
    }
    public static object Report()
    {
        var app = UnityEngine.Object.FindAnyObjectByType<DriftApplication>();
        return new {
            screen = app.Screen.ToString(), stage = app.State.Stage.ToString(),
            food = app.State.IsDone(TaskId.Food), purifier = app.State.IsDone(TaskId.Purifier),
            fuel = app.State.IsDone(TaskId.Fuel), deck = app.State.IsDone(TaskId.Deck),
            west = app.State.WestReached, broken = app.State.PurifierBroken, repaired = app.State.PurifierRepaired,
            heading = app.World.Heading,
            fish = app.State.Count(Item.Fish), rod = app.State.Count(Item.FishingRod),
            target = UnityEngine.Object.FindAnyObjectByType<DriftView>().Target()?.Kind.ToString()
        };
    }
}
