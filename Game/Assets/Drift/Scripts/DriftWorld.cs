using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Drift
{
    public sealed class DriftWorld
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly List<DriftInteractable> stations = new List<DriftInteractable>();
        private readonly List<DriftInteractable> drops = new List<DriftInteractable>();
        private readonly Material steel, rust, wood, cream, black, water, markerMaterial;
        public Transform Ship { get; private set; }
        public Transform Gull { get; private set; }
        public float Heading => Ship.eulerAngles.y;
        public IReadOnlyList<DriftInteractable> Stations => stations;
        private readonly Transform root;
        private float gullFlight = -1;
        private GameObject radioWarning;

        public DriftWorld(Transform parent, Material template, TMP_FontAsset font)
        {
            root = new GameObject("Drift Environment").transform;
            root.SetParent(parent, false);
            steel = MakeMaterial(template, new Color(.21f, .29f, .29f));
            rust = MakeMaterial(template, new Color(.44f, .20f, .105f));
            wood = MakeMaterial(template, new Color(.28f, .21f, .14f));
            cream = MakeMaterial(template, new Color(.79f, .73f, .56f));
            black = MakeMaterial(template, new Color(.055f, .075f, .08f));
            water = MakeMaterial(template, new Color(.045f, .24f, .30f));
            markerMaterial = MakeMaterial(template, Color.white);
            Ship = new GameObject("Rustbound Cargo Vessel").transform;
            Ship.SetParent(root, false);
            Ship.localPosition = new Vector3(0, 1.2f, 0);
            Box("Ocean", root, new Vector3(0, -.6f, 0), new Vector3(1600, .2f, 1600), water, false);
            Box("Hull", Ship, new Vector3(0, -.4f, 0), new Vector3(12, 2.4f, 30), rust);
            Box("Weathered Deck", Ship, new Vector3(0, .85f, 0), new Vector3(12.2f, .25f, 30), steel);
            for (int i = 0; i < 15; i++) Box("Deck Seam", Ship, new Vector3(0, .987f, -14 + i * 2), new Vector3(12, .015f, .035f), rust, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Safety Rail", Ship, new Vector3(side * 5.95f, 1.95f, 0), new Vector3(.12f, .12f, 29.8f), rust);
                Box("Railing Collision", Ship, new Vector3(side * 6, 1.55f, 0), new Vector3(.15f, 1.1f, 30), steel);
            }
            Box("Bow Rail", Ship, new Vector3(0, 1.6f, -14.9f), new Vector3(12, 1.1f, .15f), steel);
            Box("Stern Rail", Ship, new Vector3(0, 1.6f, 14.9f), new Vector3(12, 1.1f, .15f), steel);
            Box("Cabin Left", Ship, new Vector3(-4.7f, 2.5f, 8.8f), new Vector3(.3f, 3, 8), steel);
            Box("Cabin Right", Ship, new Vector3(4.7f, 2.5f, 8.8f), new Vector3(.3f, 3, 8), steel);
            Box("Cabin Back", Ship, new Vector3(0, 2.5f, 12.7f), new Vector3(9.5f, 3, .3f), steel);
            Box("Cabin Front Left", Ship, new Vector3(-3.4f, 2.5f, 4.8f), new Vector3(2.5f, 3, .25f), steel);
            Box("Cabin Front Right", Ship, new Vector3(3.4f, 2.5f, 4.8f), new Vector3(2.5f, 3, .25f), steel);
            Box("Cabin Roof", Ship, new Vector3(0, 4.1f, 8.8f), new Vector3(9.9f, .25f, 8.3f), rust);
            var lamp = new GameObject("Cabin Lamp").AddComponent<Light>();
            lamp.transform.SetParent(Ship, false); lamp.transform.localPosition = new Vector3(0, 3.7f, 8);
            lamp.type = LightType.Point; lamp.range = 12; lamp.intensity = 3; lamp.color = new Color(1, .82f, .56f);
            Box("Console", Ship, new Vector3(0, 1.4f, 10.8f), new Vector3(7, .8f, 1.1f), wood);
            AddStation("Helm", Station.Helm, new Vector3(-2, 2, 10.1f), new Vector3(.8f, .75f, .22f), wood);
            AddStation("Radio", Station.Radio, new Vector3(2, 2, 10.5f), new Vector3(.85f, .6f, .45f), black);
            Box("Radio Dial", Ship, new Vector3(2, 2.05f, 10.25f), new Vector3(.5f, .15f, .03f), cream, false);
            var warning = new GameObject("Radio Warning", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            warning.transform.SetParent(Ship, false); warning.transform.localPosition = new Vector3(2, 2.9f, 10.2f);
            warning.font = font; warning.text = "!"; warning.fontSize = 6; warning.color = new Color(1, .65f, .22f);
            warning.alignment = TextAlignmentOptions.Center; warning.rectTransform.sizeDelta = new Vector2(.7f, .8f);
            radioWarning = warning.gameObject; radioWarning.SetActive(false);
            Box("Compass", Ship, new Vector3(0, 1.9f, 10.5f), new Vector3(.35f, .1f, .35f), cream, false);
            AddStation("Daily Work Note", Station.Note, new Vector3(-4.5f, 2.5f, 7), new Vector3(.09f, .85f, .65f), cream);
            AddStation("Rain Barrel", Station.Barrel, new Vector3(-4, 1.5f, -6), new Vector3(.9f, 1.1f, .9f), steel);
            AddStation("Purifier", Station.Purifier, new Vector3(4, 1.5f, -5), new Vector3(1.3f, 1, .85f), cream);
            AddStation("Engine Hatch", Station.Engine, new Vector3(4, 1.2f, 1), new Vector3(1.7f, .45f, 1.7f), black);
            AddStation("Damaged Deck", Station.DeckRepair, new Vector3(-4, 1.02f, 0), new Vector3(1.6f, .12f, 1.5f), wood);
            AddStation("Fishing Spot", Station.Fishing, new Vector3(-3.8f, 1.55f, -12.5f), new Vector3(.25f, 1, .25f), wood);
            Box("Old Chair Seat", Ship, new Vector3(-3, 1.5f, -9), new Vector3(.7f, .12f, .7f), wood);
            Box("Old Chair Back", Ship, new Vector3(-3, 1.9f, -8.7f), new Vector3(.7f, .8f, .1f), wood);
            for (int i = 0; i < 4; i++) Box("Chair Leg", Ship, new Vector3(-3 + (i % 2 == 0 ? -.25f : .25f), 1.2f, -9 + (i < 2 ? -.25f : .25f)), new Vector3(.08f, .5f, .08f), wood);
            AddPickup(Item.FishingRod, new Vector3(-2.1f, 1.2f, -10.5f), false);
            AddPickup(Item.Scrap, new Vector3(-3, 1.2f, 2.5f), false);
            AddPickup(Item.Fuel, new Vector3(3, 1.25f, 2.5f), false);
            AddPickup(Item.PurifierPart, new Vector3(3, 1.2f, -7.5f), false);
            Gull = new GameObject("Seagull Silhouette").transform;
            Gull.SetParent(Ship, false);
            Box("Gull Body", Gull, Vector3.zero, new Vector3(.25f, .2f, .55f), cream, false);
            Box("Gull Wings", Gull, Vector3.zero, new Vector3(1.7f, .08f, .25f), cream, false);
            Gull.gameObject.SetActive(false);
            var sun = new GameObject("Sea Daylight").AddComponent<Light>();
            sun.transform.SetParent(root, false); sun.type = LightType.Directional; sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(32, -28, 0); sun.color = new Color(1, .88f, .7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .49f, .51f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.30f, .45f, .49f); RenderSettings.fogDensity = .006f;
        }
        private Material MakeMaterial(Material template, Color color)
        {
            Material material = new Material(template); material.color = color; materials.Add(material); return material;
        }
        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { obj.GetComponent<Collider>().enabled = false; Object.Destroy(obj.GetComponent<Collider>()); }
            return obj;
        }
        private DriftInteractable AddStation(string name, Station station, Vector3 position, Vector3 scale, Material material, Item item = Item.None, bool dropped = false)
        {
            GameObject obj = Box(name, Ship, position, scale, material);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Objective Marker"; marker.transform.SetParent(Ship, false); marker.transform.localPosition = position + Vector3.up * .7f;
            marker.transform.localScale = Vector3.one * .13f; marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            marker.GetComponent<Collider>().enabled = false; Object.Destroy(marker.GetComponent<Collider>()); marker.SetActive(false);
            // Parent markers to pickups so collection also hides their marker; preserve world scale.
            marker.transform.SetParent(obj.transform, true);
            var interaction = obj.AddComponent<DriftInteractable>(); interaction.Initialize(station, item, dropped, marker);
            if (dropped) drops.Add(interaction); else stations.Add(interaction);
            return interaction;
        }
        public bool CanDrop => drops.Count < 64;
        public void AddPickup(Item item, Vector3 shipLocalPosition, bool dropped)
        {
            AddStation(item.ToString(), Station.Pickup, shipLocalPosition, item == Item.FishingRod ? new Vector3(.08f, .08f, 1.5f) : Vector3.one * .32f, cream, item, dropped);
        }
        public void Collect(DriftInteractable target)
        {
            if (target.Dropped) { drops.Remove(target); Object.Destroy(target.gameObject); }
            else target.gameObject.SetActive(false);
        }
        public void Reset()
        {
            Ship.rotation = Quaternion.identity;
            for (int i = 0; i < drops.Count; i++) if (drops[i] != null) Object.Destroy(drops[i].gameObject);
            drops.Clear();
            foreach (var station in stations) { station.gameObject.SetActive(true); station.ShowMarker(false); }
            gullFlight = -1; Gull.gameObject.SetActive(false);
            radioWarning.SetActive(false);
        }
        public void Steer(float input, float dt) => Ship.Rotate(Vector3.up, input * 22 * dt, Space.World);
        public void StartGullStrike() { gullFlight = 0; Gull.gameObject.SetActive(true); }
        public void Tick(float dt)
        {
            if (gullFlight < 0) return;
            gullFlight += dt;
            float t = Mathf.Clamp01(gullFlight / 2);
            Gull.localPosition = Vector3.Lerp(new Vector3(-8, 9, -12), new Vector3(4, 2.2f, -5), t);
            if (gullFlight > 2) Gull.localPosition += new Vector3((gullFlight - 2) * 6, (gullFlight - 2) * 4, 0);
            if (gullFlight > 5) { gullFlight = -1; Gull.gameObject.SetActive(false); }
        }
        public bool InsideWheelhouse(Vector3 position)
        {
            Vector3 local = Ship.InverseTransformPoint(position);
            return Mathf.Abs(local.x) < 4.65f && local.z > 4.8f && local.z < 12.6f;
        }
        public void RefreshMarkers(DriftState state)
        {
            radioWarning.SetActive(state.Stage == StoryStage.Radio);
            foreach (var station in stations)
            {
                bool show = false;
                if (state.Stage == StoryStage.FindNote) show = station.Kind == Station.Note;
                else if (state.Stage == StoryStage.DailyWork)
                {
                    show = station.Kind == Station.Fishing && !state.IsDone(TaskId.Food)
                        || station.Kind == Station.Purifier && !state.IsDone(TaskId.Purifier)
                        || station.Kind == Station.Engine && !state.IsDone(TaskId.Fuel)
                        || station.Kind == Station.DeckRepair && !state.IsDone(TaskId.Deck);
                }
                else if (state.Stage == StoryStage.Radio) show = station.Kind == Station.Radio;
                else if (state.Stage == StoryStage.Emergency) show = station.Kind == Station.Helm && !state.WestReached || station.Kind == Station.Purifier && state.PurifierBroken;
                station.ShowMarker(show);
                if (station.Kind == Station.DeckRepair) station.GetComponent<Renderer>().sharedMaterial = state.IsDone(TaskId.Deck) ? steel : wood;
            }
        }
        public void Dispose()
        {
            foreach (var material in materials) Object.Destroy(material);
            materials.Clear();
        }
    }
}
