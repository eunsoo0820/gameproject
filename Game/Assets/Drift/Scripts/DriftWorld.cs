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
        private Light daylight;
        private float flashRemaining;
        private ParticleSystem stormRain;
        private DriftInteractable rainBarrel, fishingRod;
        public Transform Ship { get; private set; }
        public Transform Gull { get; private set; }
        public float Heading => Ship.eulerAngles.y;
        public IReadOnlyList<DriftInteractable> Stations => stations;
        private readonly Transform root;
        private float gullFlight = -1;
        private GameObject radioWarning;
        private Transform deckHatchPivot, diveHatchPivot;
        private bool deckHatchOpen, diveHatchOpen;
        private readonly Vector3 deckLadderPosition = new Vector3(-2.2f, 0, -3.5f);
        private readonly Vector3 lowerLadderPosition = new Vector3(2.2f, 0, 3.2f);
        public bool DeckHatchOpen => deckHatchOpen;
        public bool DiveHatchOpen => diveHatchOpen;

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
            Material amber = MakeMaterial(template, new Color(.95f, .52f, .16f));
            markerMaterial = MakeMaterial(template, Color.white);
            Ship = new GameObject("Weathered Three-Deck Vessel").transform;
            Ship.SetParent(root, false);
            Ship.localPosition = new Vector3(0, 1.2f, 0);
            Box("Ocean", root, new Vector3(0, .4f, 0), new Vector3(1600, .2f, 1600), water, false);
            BuildHull();
            BuildDeckHatches(amber);
            BuildWheelhouse(amber);
            BuildInterior(amber);
            BuildLadder("Deck to middle ladder", deckLadderPosition, .92f, -1.02f, Station.LadderDeckMiddle);
            BuildLadder("Middle to lower ladder", lowerLadderPosition, -1.12f, -3.12f, Station.LadderMiddleLower);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Safety Rail", Ship, new Vector3(side * 5.95f, 2.03f, 0), new Vector3(.12f, .12f, 29.8f), rust);
                Box("Railing Collision", Ship, new Vector3(side * 6, 1.55f, 0), new Vector3(.15f, 1.1f, 30), steel);
            }
            Box("Bow Rail", Ship, new Vector3(0, 1.58f, -14.9f), new Vector3(12, 1.1f, .15f), steel);
            Box("Stern Rail", Ship, new Vector3(0, 1.58f, 14.9f), new Vector3(12, 1.1f, .15f), steel);
            BuildWheelhouseStairs();
            Box("Wheelhouse Console", Ship, new Vector3(0, 2.4f, 4.2f), new Vector3(4.2f, .65f, .9f), wood);
            AddStation("Helm", Station.Helm, new Vector3(-.65f, 3.02f, 4.45f), new Vector3(.9f, .8f, .35f), wood);
            AddStation("Radio", Station.Radio, new Vector3(1.45f, 3.02f, 4.35f), new Vector3(.8f, .7f, .5f), black);
            var warning = new GameObject("Radio Warning", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            warning.transform.SetParent(Ship, false); warning.transform.localPosition = new Vector3(1.45f, 3.65f, 4.2f);
            warning.font = font; warning.text = "!"; warning.fontSize = 6; warning.color = new Color(1, .65f, .22f);
            warning.alignment = TextAlignmentOptions.Center; warning.rectTransform.sizeDelta = new Vector2(.7f, .8f);
            radioWarning = warning.gameObject; radioWarning.SetActive(false);
            Box("Radio Dial", Ship, new Vector3(1.45f, 2.95f, 4.05f), new Vector3(.5f, .15f, .03f), cream, false);
            Box("Compass", Ship, new Vector3(0, 2.82f, 4.55f), new Vector3(.35f, .1f, .35f), cream, false);
            AddStation("Daily Work Note", Station.Note, new Vector3(-2.43f, 3.1f, 2.35f), new Vector3(.12f, .8f, .75f), cream);
            rainBarrel = AddStation("Rain Barrel", Station.Barrel, new Vector3(-4, 1.5f, -6), new Vector3(.9f, 1.1f, .9f), steel);
            AddStation("Purifier", Station.Purifier, new Vector3(4, 1.5f, -5), new Vector3(1.3f, 1, .85f), cream);
            AddStation("Damaged Deck", Station.DeckRepair, new Vector3(-4, 1.02f, 0), new Vector3(1.6f, .12f, 1.5f), wood);
            AddStation("Fishing Spot", Station.Fishing, new Vector3(-3.8f, 1.55f, -12.5f), new Vector3(.25f, 1, .25f), wood);
            Box("Old Chair Seat", Ship, new Vector3(-3, 1.5f, -9), new Vector3(.7f, .12f, .7f), wood);
            Box("Old Chair Back", Ship, new Vector3(-3, 1.9f, -8.7f), new Vector3(.7f, .8f, .1f), wood);
            for (int i = 0; i < 4; i++) Box("Chair Leg", Ship, new Vector3(-3 + (i % 2 == 0 ? -.25f : .25f), 1.2f, -9 + (i < 2 ? -.25f : .25f)), new Vector3(.08f, .5f, .08f), wood);
            fishingRod = AddPickup(Item.FishingRod, new Vector3(-2.1f, 1.2f, -10.5f), false);
            AddPickup(Item.Scrap, new Vector3(-3, 1.2f, 2.5f), false);
            AddPickup(Item.Fuel, new Vector3(-4.4f, -3.05f, -8.5f), false);
            Gull = new GameObject("Seagull Silhouette").transform;
            Gull.SetParent(Ship, false);
            Box("Gull Body", Gull, Vector3.zero, new Vector3(.25f, .2f, .55f), cream, false);
            Box("Gull Wings", Gull, Vector3.zero, new Vector3(1.7f, .08f, .25f), cream, false);
            Gull.gameObject.SetActive(false);
            daylight = new GameObject("Sea Daylight").AddComponent<Light>();
            daylight.transform.SetParent(root, false); daylight.type = LightType.Directional; daylight.intensity = 1.4f;
            daylight.transform.rotation = Quaternion.Euler(32, -28, 0); daylight.color = new Color(1, .88f, .7f);
            CreateStormRain();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .49f, .51f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.30f, .45f, .49f); RenderSettings.fogDensity = .006f;
        }
        private void BuildHull()
        {
            const float halfWidth = 5.8f, halfLength = 14.5f;
            const float deckHoleX = -2.2f, deckHoleZ = -3.5f, holeWidth = 1.45f, holeLength = 1.65f;
            const float lowerHoleX = 2.2f, lowerHoleZ = 3.2f;
            FloorWithOpening("Weathered main deck", .85f, .25f, halfWidth, halfLength, deckHoleX, deckHoleZ, holeWidth, holeLength, steel);
            FloorWithOpening("Middle deck", -1.25f, .2f, halfWidth, halfLength, lowerHoleX, lowerHoleZ, holeWidth, holeLength, steel);
            Box("Lowest deck", Ship, new Vector3(0, -3.35f, 0), new Vector3(11.6f, .2f, 29), steel);
            Box("Keel", Ship, new Vector3(0, -3.55f, 0), new Vector3(8.8f, .4f, 27), rust);

            // The upper shell is continuous; the lower starboard panel leaves one sealed dive-hatch opening.
            foreach (int side in new[] { -1, 1 })
            {
                Box(side < 0 ? "Port upper hull" : "Starboard upper hull", Ship,
                    new Vector3(side * 5.8f, -.075f, 0), new Vector3(.4f, 2.1f, 29), steel);
                if (side < 0)
                    Box("Port lower hull", Ship, new Vector3(-5.8f, -2.2f, 0), new Vector3(.4f, 2.1f, 29), rust);
                else
                {
                    Box("Starboard lower hull aft", Ship, new Vector3(5.8f, -2.2f, -3.35f), new Vector3(.4f, 2.1f, 22.3f), rust);
                    Box("Starboard lower hull bow", Ship, new Vector3(5.8f, -2.2f, 12.05f), new Vector3(.4f, 2.1f, 4.9f), rust);
                }
            }
            Box("Bow bulkhead", Ship, new Vector3(0, -1.175f, -14.5f), new Vector3(11.6f, 4.35f, .4f), rust);
            Box("Stern bulkhead", Ship, new Vector3(0, -1.175f, 14.5f), new Vector3(11.6f, 4.35f, .4f), rust);
            for (int i = 0; i < 15; i++)
                Box("Deck seam", Ship, new Vector3(0, .982f, -14 + i * 2), new Vector3(11.5f, .012f, .035f), rust, false);
            for (int level = 0; level < 2; level++)
            {
                float y = level == 0 ? -.99f : -3.09f;
                for (int i = 0; i < 8; i++)
                    Box("Interior floor seam", Ship, new Vector3(0, y, -12.25f + i * 3.5f), new Vector3(11.4f, .012f, .035f), rust, false);
            }
        }
        private void FloorWithOpening(string name, float centerY, float thickness, float halfWidth, float halfLength,
            float openingX, float openingZ, float openingWidth, float openingLength, Material material)
        {
            float left = openingX - openingWidth * .5f, right = openingX + openingWidth * .5f;
            float near = openingZ - openingLength * .5f, far = openingZ + openingLength * .5f;
            Box(name + " port panel", Ship, new Vector3((-halfWidth + left) * .5f, centerY, 0), new Vector3(halfWidth + left, thickness, halfLength * 2), material);
            Box(name + " starboard panel", Ship, new Vector3((right + halfWidth) * .5f, centerY, 0), new Vector3(halfWidth - right, thickness, halfLength * 2), material);
            Box(name + " aft panel", Ship, new Vector3(openingX, centerY, (-halfLength + near) * .5f), new Vector3(openingWidth, thickness, halfLength + near), material);
            Box(name + " bow panel", Ship, new Vector3(openingX, centerY, (far + halfLength) * .5f), new Vector3(openingWidth, thickness, halfLength - far), material);
        }
        private void BuildDeckHatches(Material amber)
        {
            Vector3 deckHole = new Vector3(deckLadderPosition.x, .975f, deckLadderPosition.z);
            deckHatchPivot = new GameObject("Deck hatch hinge").transform;
            deckHatchPivot.SetParent(Ship, false);
            deckHatchPivot.localPosition = deckHole + new Vector3(0, .02f, .825f);
            Box("Closed deck hatch", deckHatchPivot, new Vector3(0, 0, -.825f), new Vector3(1.45f, .12f, 1.65f), rust);
            Box("Deck hatch wheel", deckHatchPivot, new Vector3(0, .1f, -.825f), new Vector3(.48f, .07f, .48f), amber, false);
            AddStation("Deck hatch control", Station.DeckHatch, new Vector3(-.95f, 1.35f, -3.5f), new Vector3(.28f, .68f, .28f), amber);

            diveHatchPivot = new GameObject("Dive hatch hinge").transform;
            diveHatchPivot.SetParent(Ship, false);
            diveHatchPivot.localPosition = new Vector3(5.8f, -2.2f, 7.8f);
            Box("Dive chamber watertight door", diveHatchPivot, new Vector3(0, 0, .9f), new Vector3(.14f, 1.8f, 1.8f), rust);
            Box("Dive hatch wheel", diveHatchPivot, new Vector3(-.1f, 0, .9f), new Vector3(.12f, .48f, .48f), amber, false);
            AddStation("Dive hatch control", Station.DiveHatch, new Vector3(4.9f, -2.45f, 8.7f), new Vector3(.28f, .7f, .28f), amber);
        }
        private void BuildLadder(string name, Vector3 position, float upperY, float lowerY, Station station)
        {
            float height = upperY - lowerY;
            float middleY = (upperY + lowerY) * .5f;
            Box(name + " port rail", Ship, position + new Vector3(-.34f, middleY, 0), new Vector3(.09f, height, .1f), rust);
            GameObject climbRail = Box(name + " interaction rail", Ship, position + new Vector3(.34f, middleY, 0), new Vector3(.09f, height, .1f), rust);
            AddInteraction(climbRail, station);
            for (float y = lowerY + .18f; y < upperY; y += .24f)
                Box(name + " rung", Ship, position + new Vector3(0, y, 0), new Vector3(.72f, .075f, .12f), cream);
        }
        private void BuildWheelhouse(Material amber)
        {
            const float cx = 0, cz = 2.4f, halfX = 2.6f, frontZ = -.35f, backZ = 5.15f;
            Box("Raised wheelhouse floor", Ship, new Vector3(cx, 2.08f, cz), new Vector3(5.2f, .18f, 5.5f), wood);
            Box("Wheelhouse port wall", Ship, new Vector3(-halfX, 3.22f, cz), new Vector3(.18f, 2.2f, 5.5f), steel);
            Box("Wheelhouse starboard forward wall", Ship, new Vector3(halfX, 3.22f, .5f), new Vector3(.18f, 2.2f, 1.7f), steel);
            Box("Wheelhouse starboard aft wall", Ship, new Vector3(halfX, 3.22f, 4.3f), new Vector3(.18f, 2.2f, 1.7f), steel);
            Box("Wheelhouse aft wall", Ship, new Vector3(0, 3.22f, frontZ), new Vector3(5.2f, 2.2f, .18f), steel);
            Box("Wheelhouse bow wall", Ship, new Vector3(0, 3.22f, backZ), new Vector3(5.2f, 2.2f, .18f), steel);
            Box("Wheelhouse roof", Ship, new Vector3(0, 4.34f, cz), new Vector3(5.45f, .18f, 5.75f), rust);
            for (int i = 0; i < 3; i++)
                Box("Forward bridge window", Ship, new Vector3(-1.6f + i * 1.6f, 3.45f, backZ - .1f), new Vector3(1.1f, .72f, .04f), black, false);
            for (int i = 0; i < 3; i++)
                Box("Port bridge window", Ship, new Vector3(-halfX + .1f, 3.45f, .55f + i * 1.65f), new Vector3(.04f, .72f, 1.05f), black, false);
            AddLamp(new Vector3(0, 3.75f, 2.4f), amber, 6, 1.1f);
        }
        private void BuildWheelhouseStairs()
        {
            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float rise = .15f;
                float top = .975f + (i + 1) * rise;
                float x = 4.45f - i * .19f;
                Box("Wheelhouse stair", Ship, new Vector3(x, top - rise * .5f, 2.4f), new Vector3(.42f, rise, 1.25f), rust);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = Box("Wheelhouse stair handrail", Ship, new Vector3(3.78f, 1.72f, 2.4f + side * .7f), new Vector3(1.9f, .08f, .08f), cream, false);
                rail.transform.localRotation = Quaternion.Euler(0, 0, -38f);
            }
        }
        private void BuildInterior(Material amber)
        {
            // Middle-deck rooms remain open enough for navigation between the offset ladders.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = -10 + i * 6.2f;
                    Box("Middle deck locker", Ship, new Vector3(side * 4.75f, -.55f, z), new Vector3(.8f, 1.1f, 1.2f), black);
                    Box("Middle deck porthole", Ship, new Vector3(side * 5.55f, -.25f, z + 2.2f), new Vector3(.025f, .48f, .48f), cream, false);
                }
            }
            AddLamp(new Vector3(-3.8f, -.4f, -7), amber, 5, .8f);
            AddLamp(new Vector3(3.8f, -.4f, 0), amber, 5, .8f);
            AddLamp(new Vector3(-3.8f, -.4f, 8), amber, 5, .8f);

            // Engine room at the aft end of the lowest deck.
            Box("Main diesel engine", Ship, new Vector3(-3.45f, -2.58f, -8.5f), new Vector3(2.4f, 1.25f, 3.4f), black);
            Box("Engine block top", Ship, new Vector3(-3.45f, -1.9f, -8.5f), new Vector3(1.45f, .18f, 2.7f), rust);
            for (int i = 0; i < 4; i++)
                Box("Engine pipe", Ship, new Vector3(-1.9f + i * .35f, -2.3f, -8.5f), new Vector3(.14f, 1.25f, .14f), rust);
            AddStation("Engine fuel valve", Station.Engine, new Vector3(-1.9f, -2.6f, -6.7f), new Vector3(.75f, .75f, .5f), amber);
            Box("Dive chamber aft divider", Ship, new Vector3(1.2f, -2.45f, 7.1f), new Vector3(.16f, 1.5f, 1.4f), steel);
            Box("Dive chamber bow divider", Ship, new Vector3(1.2f, -2.45f, 11.2f), new Vector3(.16f, 1.5f, 2.6f), steel);
            Box("Dive chamber aft bulkhead", Ship, new Vector3(3.5f, -2.45f, 6.3f), new Vector3(4.6f, 1.5f, .16f), steel);
            Box("Dive chamber bow bulkhead", Ship, new Vector3(3.5f, -2.45f, 12.8f), new Vector3(4.6f, 1.5f, .16f), steel);
            AddLamp(new Vector3(-3.8f, -2.7f, -8), amber, 5, 1.0f);
            AddLamp(new Vector3(3.8f, -2.7f, 8), amber, 5, 1.0f);
        }
        private void AddLamp(Vector3 localPosition, Material material, float range, float intensity)
        {
            Box("Warm utility lamp", Ship, localPosition, new Vector3(.18f, .24f, .18f), material, false);
            var lamp = new GameObject("Utility light").AddComponent<Light>();
            lamp.transform.SetParent(Ship, false); lamp.transform.localPosition = localPosition;
            lamp.type = LightType.Point; lamp.range = range; lamp.intensity = intensity; lamp.color = new Color(1, .72f, .42f);
        }
        private void CreateStormRain()
        {
            var rainObject = new GameObject("Thunderstorm Rain", typeof(ParticleSystem));
            rainObject.transform.SetParent(Ship, false); rainObject.transform.localPosition = new Vector3(0, 9, 0);
            stormRain = rainObject.GetComponent<ParticleSystem>();
            var main = stormRain.main; main.loop = true; main.startLifetime = 1.15f; main.startSpeed = 15f;
            main.startSize = .025f; main.startColor = new Color(.68f, .78f, .82f, .75f); main.maxParticles = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = stormRain.emission; emission.rateOverTime = 280; emission.enabled = false;
            var shape = stormRain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(18, .4f, 36);
            var renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader != null) renderer.sharedMaterial = new Material(particleShader);
            stormRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private DriftInteractable AddInteraction(GameObject obj, Station station)
        {
            var interaction = obj.AddComponent<DriftInteractable>();
            interaction.Initialize(station, Item.None, false, null);
            stations.Add(interaction);
            return interaction;
        }
        public void SetDeckHatchOpen(bool open)
        {
            deckHatchOpen = open;
            if (deckHatchPivot != null) deckHatchPivot.localRotation = open ? Quaternion.Euler(-100, 0, 0) : Quaternion.identity;
        }
        public void SetDiveHatchOpen(bool open)
        {
            diveHatchOpen = open;
            if (diveHatchPivot != null) diveHatchPivot.localRotation = open ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
        }
        public bool TryGetLadderDestination(Station station, float currentY, out Vector3 destination)
        {
            if (station == Station.LadderDeckMiddle)
            {
                bool descending = currentY > 0f;
                destination = new Vector3(deckLadderPosition.x + .9f, descending ? -1.02f : 1.1f, deckLadderPosition.z);
                return true;
            }
            if (station == Station.LadderMiddleLower)
            {
                bool descending = currentY > -2.1f;
                destination = new Vector3(lowerLadderPosition.x + .9f, descending ? -3.12f : -1.02f, lowerLadderPosition.z);
                return true;
            }
            destination = Vector3.zero;
            return false;
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
        public bool IsExposed(Vector3 worldPosition) => Ship.InverseTransformPoint(worldPosition).y > .3f && !InsideWheelhouse(worldPosition);
        public void SpawnPurifierPartAtSea() => AddPickup(Item.PurifierPart, new Vector3(7.15f, -1.1f, Random.Range(8.2f, 9.4f)), true);
        public void SetStorm(bool active)
        {
            RenderSettings.ambientLight = active ? new Color(.18f, .23f, .27f) : new Color(.40f, .49f, .51f);
            RenderSettings.fogColor = active ? new Color(.13f, .2f, .24f) : new Color(.30f, .45f, .49f);
            if (daylight != null) daylight.intensity = active ? .38f : 1.4f;
            if (stormRain != null)
            {
                var emission = stormRain.emission; emission.enabled = active;
                if (active && !stormRain.isPlaying) stormRain.Play();
                if (!active) stormRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            flashRemaining = 0;
        }
        public void FlashLightning() { if (daylight != null) { flashRemaining = .18f; daylight.intensity = 3.5f; } }
        public DriftInteractable AddPickup(Item item, Vector3 shipLocalPosition, bool dropped)
        {
            return AddStation(item.ToString(), Station.Pickup, shipLocalPosition, item == Item.FishingRod ? new Vector3(.08f, .08f, 1.5f) : Vector3.one * .32f, cream, item, dropped);
        }
        public void Collect(DriftInteractable target)
        {
            if (target.Dropped) { drops.Remove(target); Object.Destroy(target.gameObject); }
            else target.gameObject.SetActive(false);
        }
        public void Reset()
        {
            Ship.rotation = Quaternion.identity;
            SetDeckHatchOpen(false);
            SetDiveHatchOpen(false);
            for (int i = 0; i < drops.Count; i++) if (drops[i] != null) Object.Destroy(drops[i].gameObject);
            drops.Clear();
            foreach (var station in stations) { station.gameObject.SetActive(true); station.ShowMarker(false); }
            rainBarrel.transform.localPosition = new Vector3(Random.Range(-4.4f, 4.4f), 1.5f, Random.Range(-12f, 12f));
            fishingRod.transform.localPosition = new Vector3(Random.Range(-4.4f, 4.4f), 1.2f, Random.Range(-12f, 12f));
            gullFlight = -1; Gull.gameObject.SetActive(false);
            radioWarning.SetActive(false);
            SetStorm(false);
        }
        public void Steer(float input, float dt) => Ship.Rotate(Vector3.up, input * 22 * dt, Space.World);
        public void StartGullStrike() { gullFlight = 0; Gull.gameObject.SetActive(true); }
        public void Tick(float dt)
        {
            if (flashRemaining > 0) { flashRemaining -= dt; if (flashRemaining <= 0 && daylight != null) daylight.intensity = .38f; }
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
            return local.y > 2.0f && Mathf.Abs(local.x) < 2.55f && local.z > -.2f && local.z < 5.0f;
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
                else if (state.Stage == StoryStage.SteerWest) show = station.Kind == Station.Helm;
                else if (state.Stage == StoryStage.RepairPurifier) show = station.Kind == Station.Purifier;
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
