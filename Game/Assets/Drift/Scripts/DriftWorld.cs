using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Drift
{
    public sealed class DriftWorld
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Vector3> waterBaseVertices = new List<Vector3>();
        private readonly List<Vector3> waterVertices = new List<Vector3>();
        private readonly List<Vector3> hullWaterline = new List<Vector3>();
        private readonly List<DriftInteractable> stations = new List<DriftInteractable>();
        private readonly List<DriftInteractable> drops = new List<DriftInteractable>();
        private readonly Material steel, rust, wood, cream, black, water, markerMaterial, teal, safetyOrange;
        private Mesh roundedBoxMesh, waterMesh;
        private Transform waterSurface;
        private Transform wheelhouseRoot;
        private Light daylight;
        private float flashRemaining;
        private float waterTime;
        private float shipSpeed;
        private bool stormActive;
        private ParticleSystem stormRain;
        private ParticleSystem shipWake;
        private DriftInteractable rainBarrel, fishingRod;
        public Transform Ship { get; private set; }
        public Transform Gull { get; private set; }
        public float Heading => Mathf.Repeat(Ship.eulerAngles.y + 180f, 360f);
        public IReadOnlyList<DriftInteractable> Stations => stations;
        private readonly Transform root;
        private float gullFlight = -1;
        private GameObject radioWarning;
        private Transform deckHatchPivot, diveHatchPivot;
        private bool deckHatchOpen, diveHatchOpen;
        private readonly Vector3 deckLadderPosition = new Vector3(-2.2f, 0, -3.5f);
        private readonly Vector3 lowerLadderPosition = new Vector3(2.2f, 0, 3.2f);
        private const float MainDeckY = .85f, MiddleDeckY = -2.15f, LowerDeckY = -5.15f;
        private const float WaterSurfaceLocalY = .4f, ShipRestY = 1.2f;
        private const float OceanExtent = 2048f, OceanStep = 1f;
        public bool DeckHatchOpen => deckHatchOpen;
        public bool DiveHatchOpen => diveHatchOpen;

        public DriftWorld(Transform parent, Material template, TMP_FontAsset font)
        {
            root = new GameObject("Drift Environment").transform;
            root.SetParent(parent, false);
            steel = MakeMaterial(template, new Color(.18f, .38f, .40f));
            rust = MakeMaterial(template, new Color(.86f, .34f, .18f));
            wood = MakeMaterial(template, new Color(.58f, .39f, .23f));
            cream = MakeMaterial(template, new Color(.94f, .83f, .61f));
            black = MakeMaterial(template, new Color(.075f, .12f, .15f));
            teal = MakeMaterial(template, new Color(.12f, .48f, .48f));
            safetyOrange = MakeMaterial(template, new Color(.98f, .38f, .16f));
            water = MakeMaterial(template, new Color(.10f, .39f, .48f));
            Texture2D waterPattern = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            { name = "Tiled blue-green ocean ripples", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var waterPixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 128f * Mathf.PI * 2f, v = y / 128f * Mathf.PI * 2f;
                    float ripple = Mathf.Sin(v * 2f + Mathf.Sin(u) * .6f) * .5f + Mathf.Sin(v * 5f - u * 2f) * .18f;
                    float fleck = Mathf.PerlinNoise(x * .09f, y * .09f) - .5f;
                    Color shade = new Color(.58f, .83f, .88f) + Color.white * (ripple * .075f + fleck * .035f);
                    waterPixels[y * 128 + x] = shade;
                }
            waterPattern.SetPixels32(waterPixels); waterPattern.Apply(false, true); generatedTextures.Add(waterPattern);
            if (water.HasProperty("_BaseMap")) water.SetTexture("_BaseMap", waterPattern);
            if (water.HasProperty("_MainTex")) water.SetTexture("_MainTex", waterPattern);
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", .72f);
            if (water.HasProperty("_Metallic")) water.SetFloat("_Metallic", .02f);
            Material amber = MakeMaterial(template, new Color(.95f, .52f, .16f));
            markerMaterial = MakeMaterial(template, Color.white);
            Ship = new GameObject("Weathered Three-Deck Vessel").transform;
            Ship.SetParent(root, false);
            Ship.localPosition = new Vector3(0, ShipRestY, 0);
            // Scale the complete vessel while keeping gameplay positions vessel-local.
            Ship.localScale = new Vector3(1.6f, 1.28f, 1.45f);
            wheelhouseRoot = new GameObject("Integrated wheelhouse structure").transform;
            wheelhouseRoot.SetParent(Ship, false);
            wheelhouseRoot.localPosition = new Vector3(0, 0, -.4f);
            wheelhouseRoot.localScale = new Vector3(1.25f, 1, 1.1f);
            BuildOceanSurface();
            BuildHull();
            BuildDeckHatches(amber);
            BuildWheelhouse(amber);
            BuildInterior(amber);
            BuildLadder("Deck to middle ladder", deckLadderPosition, .92f, -1.93f, Station.LadderDeckMiddle);
            BuildLadder("Middle to lower ladder", lowerLadderPosition, -1.93f, -4.93f, Station.LadderMiddleLower);
            BuildPerimeterRails();
            BuildWheelhouseStairs();
            BuildDeckDetails();
            BuildWheelhouseConsole();
            AddStation("Helm", Station.Helm, new Vector3(-.65f, 4.0f, .05f), new Vector3(.9f, .8f, .55f), wood, parent: wheelhouseRoot);
            AddStation("Radio", Station.Radio, new Vector3(1.45f, 3.98f, 1.25f), new Vector3(.8f, .7f, .5f), black, parent: wheelhouseRoot);
            var warning = new GameObject("Radio Warning", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            warning.transform.SetParent(wheelhouseRoot, false); warning.transform.localPosition = new Vector3(1.45f, 4.55f, 1.25f);
            warning.font = font; warning.text = "!"; warning.fontSize = 6; warning.color = new Color(1, .65f, .22f);
            warning.alignment = TextAlignmentOptions.Center; warning.rectTransform.sizeDelta = new Vector2(.7f, .8f);
            radioWarning = warning.gameObject; radioWarning.SetActive(false);
            Box("Compass", wheelhouseRoot, new Vector3(0, 3.76f, .52f), new Vector3(.35f, .1f, .35f), cream, false);
            AddStation("Daily Work Note", Station.Note, new Vector3(-2.43f, 4.02f, 2.35f), new Vector3(.12f, .8f, .75f), cream, parent: wheelhouseRoot);
            rainBarrel = AddStation("Rain Barrel", Station.Barrel, new Vector3(-4, 1.5f, -6), new Vector3(.9f, 1.1f, .9f), steel);
            AddStation("Purifier", Station.Purifier, new Vector3(4, -1.45f, -5), new Vector3(1.3f, 1, .85f), cream);
            AddStation("Damaged Deck", Station.DeckRepair, new Vector3(-4, 1.02f, 0), new Vector3(1.6f, .12f, 1.5f), wood);
            AddStation("Fishing Spot", Station.Fishing, new Vector3(-3.8f, 1.55f, -12.5f), new Vector3(.25f, 1, .25f), wood);
            Box("Old Chair Seat", Ship, new Vector3(-3, 1.5f, -9), new Vector3(.7f, .12f, .7f), wood);
            Box("Old Chair Back", Ship, new Vector3(-3, 1.9f, -8.7f), new Vector3(.7f, .8f, .1f), wood);
            for (int i = 0; i < 4; i++) Box("Chair Leg", Ship, new Vector3(-3 + (i % 2 == 0 ? -.25f : .25f), 1.2f, -9 + (i < 2 ? -.25f : .25f)), new Vector3(.08f, .5f, .08f), wood);
            fishingRod = AddPickup(Item.FishingRod, new Vector3(-2.1f, 1.2f, -10.5f), false);
            AddPickup(Item.Scrap, new Vector3(-3, 1.2f, 2.5f), false);
            AddPickup(Item.Fuel, new Vector3(-1.8f, -4.95f, -10.8f), false);
            Gull = new GameObject("Seagull Silhouette").transform;
            Gull.SetParent(Ship, false);
            Box("Gull Body", Gull, Vector3.zero, new Vector3(.25f, .2f, .55f), cream, false);
            Box("Gull Wings", Gull, Vector3.zero, new Vector3(1.7f, .08f, .25f), cream, false);
            Gull.gameObject.SetActive(false);
            daylight = new GameObject("Sea Daylight").AddComponent<Light>();
            daylight.transform.SetParent(root, false); daylight.type = LightType.Directional; daylight.intensity = 1.4f;
            daylight.transform.rotation = Quaternion.Euler(32, -28, 0); daylight.color = new Color(1, .88f, .7f);
            CreateStormRain();
            CreateShipWake();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .49f, .51f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.30f, .45f, .49f); RenderSettings.fogDensity = .006f;
            CombineStaticVisuals();
        }
        private void BuildOceanSurface()
        {
            const int outlineSamples = 64;
            for (int sample = 0; sample <= outlineSamples; sample++)
            {
                float z = Mathf.Lerp(-15.1f, 15.1f, sample / (float)outlineSamples);
                float beam = Mathf.Max(0, HullBeamAt(z) + .35f);
                hullWaterline.Add(new Vector3(-beam, 0, z));
            }
            for (int sample = outlineSamples; sample >= 0; sample--)
            {
                float z = Mathf.Lerp(-15.1f, 15.1f, sample / (float)outlineSamples);
                float beam = Mathf.Max(0, HullBeamAt(z) + .35f);
                hullWaterline.Add(new Vector3(beam, 0, z));
            }

            int rowCount = Mathf.RoundToInt(OceanExtent * 2f / OceanStep) + 1;
            var uvs = new List<Vector2>(rowCount * 4);
            var triangles = new List<int>((rowCount - 1) * 12);
            for (int row = 0; row < rowCount; row++)
            {
                float z = -OceanExtent + row * OceanStep;
                waterBaseVertices.Add(new Vector3(-OceanExtent, 0, z));
                waterBaseVertices.Add(new Vector3(0, 0, z));
                waterBaseVertices.Add(new Vector3(0, 0, z));
                waterBaseVertices.Add(new Vector3(OceanExtent, 0, z));
                for (int point = 0; point < 4; point++)
                {
                    float x = point == 0 ? -OceanExtent : point == 3 ? OceanExtent : 0;
                    uvs.Add(new Vector2(x / 20f, z / 20f));
                }
                if (row == 0) continue;
                int previous = (row - 1) * 4, current = row * 4;
                triangles.Add(previous); triangles.Add(current); triangles.Add(current + 1);
                triangles.Add(previous); triangles.Add(current + 1); triangles.Add(previous + 1);
                triangles.Add(previous + 2); triangles.Add(current + 3); triangles.Add(previous + 3);
                triangles.Add(previous + 2); triangles.Add(current + 2); triangles.Add(current + 3);
            }
            waterVertices.AddRange(waterBaseVertices);
            waterMesh = new Mesh { name = "World-anchored rippling sea with hull contact" };
            waterMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            waterMesh.SetVertices(waterVertices); waterMesh.SetUVs(0, uvs); waterMesh.SetTriangles(triangles, 0);
            waterMesh.RecalculateNormals(); waterMesh.RecalculateBounds();
            generatedMeshes.Add(waterMesh);
            waterSurface = new GameObject("World-anchored rippling sea").transform;
            waterSurface.SetParent(root, false);
            waterSurface.gameObject.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            var renderer = waterSurface.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = water;
            if (water.HasProperty("_Cull")) water.SetInt("_Cull", 0);
            UpdateOceanSurface();
        }
        private void UpdateOceanSurface()
        {
            if (waterSurface == null || waterMesh == null) return;
            waterSurface.localPosition = new Vector3(0, WaterSurfaceLocalY, 0);
            waterSurface.localRotation = Quaternion.identity;
            waterSurface.localScale = Vector3.one;
            float amplitude = stormActive ? .07f : .035f;
            float shipRootZ = root.InverseTransformPoint(Ship.position).z;
            int rowCount = waterBaseVertices.Count / 4;
            for (int row = 0; row < rowCount; row++)
            {
                int vertex = row * 4;
                float z = waterBaseVertices[vertex].z;
                float left = 0, right = 0;
                if (Mathf.Abs(z - shipRootZ) < 28f) FindHullWaterlineSpan(z, out left, out right);

                waterVertices[vertex] = new Vector3(-OceanExtent, 0, z);
                waterVertices[vertex + 1] = new Vector3(left, 0, z);
                waterVertices[vertex + 2] = new Vector3(right, 0, z);
                waterVertices[vertex + 3] = new Vector3(OceanExtent, 0, z);
                for (int pointIndex = 0; pointIndex < 4; pointIndex++)
                {
                    int index = vertex + pointIndex;
                    Vector3 point = waterVertices[index];
                    point.y = (Mathf.Sin(point.x * .18f + point.z * .035f + waterTime * .48f) +
                        .45f * Mathf.Sin(point.x * .11f + point.z * .075f - waterTime * .31f)) * amplitude;
                    waterVertices[index] = point;
                }
            }
            waterMesh.SetVertices(waterVertices);
            waterMesh.RecalculateNormals(); waterMesh.RecalculateBounds();
        }
        private void FindHullWaterlineSpan(float rootZ, out float left, out float right)
        {
            left = float.PositiveInfinity;
            right = float.NegativeInfinity;
            for (int i = 0; i < hullWaterline.Count; i++)
            {
                Vector3 world = Ship.TransformPoint(hullWaterline[i]);
                Vector3 a = root.InverseTransformPoint(world);
                Vector3 nextWorld = Ship.TransformPoint(hullWaterline[(i + 1) % hullWaterline.Count]);
                Vector3 b = root.InverseTransformPoint(nextWorld);
                if ((a.z <= rootZ && b.z > rootZ) || (b.z <= rootZ && a.z > rootZ))
                {
                    float t = (rootZ - a.z) / (b.z - a.z);
                    float x = Mathf.Lerp(a.x, b.x, t);
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }
            if (float.IsInfinity(left)) left = right = 0;
        }
        private void BuildHull()
        {
            const float halfLength = 14.5f;
            BuildHullShell();
            BuildDeckSurface("Raised weather deck", MainDeckY, .2f, halfLength, 1f,
                deckLadderPosition.x, deckLadderPosition.z, 1.6f, 1.85f, steel);
            BuildDeckSurface("Spacious middle deck", MiddleDeckY, .22f, halfLength, .84f,
                lowerLadderPosition.x, lowerLadderPosition.z, 1.55f, 1.8f, steel);
            BuildDeckSurface("Spacious lower deck", LowerDeckY, .24f, halfLength, .68f, 100, 100, 0, 0, steel);
            // The continuous hull shell encloses the lower deck; no external box keel.

            // Physical boundaries are separate from the sculpted visual shell. The starboard
            // lower section leaves a clear opening around the operable dive door.
            const int segmentCount = 19;
            const float segmentLength = 27f / segmentCount;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                float z0 = -13.5f + segment * segmentLength, z1 = z0 + segmentLength, z = (z0 + z1) * .5f;
                float beam = HullBeamAt(z) - .15f;
                foreach (int side in new[] { -1, 1 })
                {
                    AddHullBoundary(side < 0 ? "Port hull boundary" : "Starboard hull boundary",
                        new Vector3(side * beam * .96f, -.7f, z), new Vector3(.28f, 3.1f, segmentLength + .02f));
                    bool overlapsDiveDoor = z1 > 7.15f && z0 < 9.25f;
                    if (!(side > 0 && overlapsDiveDoor))
                        AddHullBoundary("Lower hull boundary", new Vector3(side * beam * .96f, -3.7f, z), new Vector3(.32f, 3.1f, segmentLength + .02f));
                }
            }
            Box("Bow inner bulkhead", Ship, new Vector3(0, -2.2f, -13.8f), new Vector3(3.6f, 5.9f, .25f), rust);
            Box("Stern inner bulkhead", Ship, new Vector3(0, -2.2f, 13.8f), new Vector3(3.6f, 5.9f, .25f), rust);
            BuildHullPortholes();
        }
        private void BuildDeckSurface(string name, float y, float thickness, float halfLength, float beamScale,
            float holeX, float holeZ, float holeWidth, float holeLength, Material material)
        {
            var rows = new List<float>();
            for (float z = -halfLength; z < halfLength; z += 2.4f) rows.Add(z);
            rows.Add(halfLength);
            if (holeLength > 0)
            { rows.Add(holeZ - holeLength * .5f); rows.Add(holeZ + holeLength * .5f); }
            rows.Sort();
            for (int i = 0; i < rows.Count - 1; i++)
            {
                float z0 = rows[i], z1 = rows[i + 1], centerZ = (z0 + z1) * .5f;
                if (z1 - z0 < .01f) continue;
                float beam = Mathf.Max(.15f, Mathf.Min(HullBeamAt(z0), HullBeamAt(z1)) * beamScale - .12f);
                bool holeBand = holeLength > 0 && centerZ > holeZ - holeLength * .5f && centerZ < holeZ + holeLength * .5f;
                if (holeBand)
                {
                    float left = holeX - holeWidth * .5f, right = holeX + holeWidth * .5f;
                    AddDeckPanel(name, y, thickness, z0, z1, (-beam + left) * .5f, left + beam, material);
                    AddDeckPanel(name, y, thickness, z0, z1, (right + beam) * .5f, beam - right, material);
                }
                else AddDeckPanel(name, y, thickness, z0, z1, 0, beam * 2, material);
            }
        }
        private void AddDeckPanel(string name, float y, float thickness, float z0, float z1, float centerX, float width, Material material)
        {
            if (width < .15f) return;
            Box(name + " plate", Ship, new Vector3(centerX, y, (z0 + z1) * .5f),
                new Vector3(width, thickness, z1 - z0 + .015f), material);
        }
        private float HullBeamAt(float z)
        {
            float[] zs = { -15.1f, -14f, -11f, -6f, 6f, 11f, 14f, 15.1f };
            float[] beams = { .4f, 2.9f, 4.9f, 5.7f, 5.7f, 4.9f, 2.9f, .4f };
            z = Mathf.Clamp(z, zs[0], zs[zs.Length - 1]);
            for (int i = 0; i < zs.Length - 1; i++)
                if (z <= zs[i + 1])
                {
                    float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(zs[i], zs[i + 1], z));
                    return Mathf.Lerp(beams[i], beams[i + 1], t);
                }
            return beams[beams.Length - 1];
        }
        private void BuildHullShell()
        {
            float[,] stations = {
                { -15.1f, .4f, -6.3f, 1.03f }, { -14.0f, 2.9f, -6.3f, .93f },
                { -11.0f, 4.9f, -6.5f, .85f }, { -6.0f, 5.7f, -6.6f, .85f },
                { 6.0f, 5.7f, -6.6f, .85f }, { 11.0f, 4.9f, -6.5f, .85f },
                { 14.0f, 2.9f, -6.3f, .93f }, { 15.1f, .4f, -6.3f, 1.03f }
            };
            float[] xProfile = { -1f, -.98f, -.95f, -.88f, -.68f, 0, .68f, .88f, .95f, .98f, 1f };
            float[] yProfile = { 0, .1f, .26f, .55f, .86f, 1f, .86f, .55f, .26f, .1f, 0 };
            var ringPositions = new List<float>(69);
            for (int i = 0; i < 65; i++) ringPositions.Add(Mathf.Lerp(stations[0, 0], stations[stations.GetLength(0) - 1, 0], i / 64f));
            ringPositions.Add(7.15f); ringPositions.Add(9.25f); ringPositions.Sort();
            const int ringCount = 67;
            int ringSize = xProfile.Length;
            var vertices = new List<Vector3>(ringCount * ringSize);
            var uvs = new List<Vector2>(ringCount * ringSize);
            for (int ring = 0; ring < ringCount; ring++)
            {
                float z = ringPositions[ring];
                int segment = 0;
                while (segment < stations.GetLength(0) - 2 && z > stations[segment + 1, 0]) segment++;
                float span = stations[segment + 1, 0] - stations[segment, 0];
                float t = Mathf.SmoothStep(0, 1, (z - stations[segment, 0]) / span);
                float beam = Mathf.Lerp(stations[segment, 1], stations[segment + 1, 1], t);
                float bottom = Mathf.Lerp(stations[segment, 2], stations[segment + 1, 2], t);
                float top = Mathf.Lerp(stations[segment, 3], stations[segment + 1, 3], t);
                for (int point = 0; point < ringSize; point++)
                {
                    vertices.Add(new Vector3(xProfile[point] * beam, Mathf.Lerp(top, bottom, yProfile[point]), z));
                    uvs.Add(new Vector2(point / (float)(ringSize - 1) * 2f, ring / (float)(ringCount - 1) * 4f));
                }
            }
            var paint = new List<int>(); var stripe = new List<int>(); var underwater = new List<int>();
            for (int ring = 0; ring < ringCount - 1; ring++)
                for (int point = 0; point < ringSize - 1; point++)
                {
                    int a = ring * ringSize + point, b = (ring + 1) * ringSize + point;
                    int c = b + 1, d = a + 1;
                    float y = (vertices[a].y + vertices[b].y + vertices[c].y + vertices[d].y) * .25f;
                    float z = (vertices[a].z + vertices[b].z) * .5f;
                    bool diveDoorOpening = z >= 7.15f && z < 9.25f && point == 6;
                    if (diveDoorOpening) continue;
                    List<int> target = y > -1.92f ? paint : y > -2.3f ? stripe : underwater;
                    target.Add(a); target.Add(b); target.Add(d); target.Add(b); target.Add(c); target.Add(d);
                }
            // Close both end rings so the interior cannot be seen through the bow or stern.
            for (int end = 0; end < 2; end++)
            {
                int start = end == 0 ? 0 : (ringCount - 1) * ringSize;
                for (int point = 1; point < ringSize - 1; point++)
                {
                    underwater.Add(start);
                    underwater.Add(start + (end == 0 ? point + 1 : point));
                    underwater.Add(start + (end == 0 ? point : point + 1));
                }
            }
            var mesh = new Mesh { name = "Tapered welded steel hull" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.subMeshCount = 3;
            mesh.SetTriangles(paint, 0); mesh.SetTriangles(stripe, 1); mesh.SetTriangles(underwater, 2);
            var normals = new List<Vector3>(vertices.Count);
            for (int ring = 0; ring < ringCount; ring++)
                for (int point = 0; point < ringSize; point++)
                {
                    int beforeRing = Mathf.Max(0, ring - 1), afterRing = Mathf.Min(ringCount - 1, ring + 1);
                    int beforePoint = Mathf.Max(0, point - 1), afterPoint = Mathf.Min(ringSize - 1, point + 1);
                    Vector3 along = vertices[afterRing * ringSize + point] - vertices[beforeRing * ringSize + point];
                    Vector3 across = vertices[ring * ringSize + afterPoint] - vertices[ring * ringSize + beforePoint];
                    Vector3 normal = Vector3.Cross(across, along).normalized;
                    normals.Add(normal.sqrMagnitude < .1f ? Vector3.up : normal);
                }
            mesh.SetNormals(normals); mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            var hull = new GameObject("Sculpted tapered hull shell"); hull.transform.SetParent(Ship, false);
            hull.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = hull.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { teal, cream, steel };
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                if (renderer.sharedMaterials[i].HasProperty("_Cull")) renderer.sharedMaterials[i].SetInt("_Cull", 0);
            // Deliberately leave the render shell non-colliding; invisible convex boundaries
            // keep the playable interiors stable and leave the dive doorway traversable.
        }
        private void AddHullBoundary(string name, Vector3 position, Vector3 size)
        {
            var boundary = Box(name, Ship, position, size, steel);
            var renderer = boundary.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false;
        }
        private void CombineStaticVisuals()
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var sourceRenderers = new List<MeshRenderer>();
            foreach (var renderer in Ship.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null || !renderer.enabled || renderer.GetComponentInParent<DriftInteractable>() != null || renderer.GetComponentInParent<TMP_Text>() != null ||
                    (Gull != null && renderer.transform.IsChildOf(Gull)) ||
                    (deckHatchPivot != null && renderer.transform.IsChildOf(deckHatchPivot)) ||
                    (diveHatchPivot != null && renderer.transform.IsChildOf(diveHatchPivot))) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Material[] sourceMaterials = renderer.sharedMaterials;
                for (int subMesh = 0; subMesh < filter.sharedMesh.subMeshCount && subMesh < sourceMaterials.Length; subMesh++)
                {
                    Material material = sourceMaterials[subMesh]; if (material == null) continue;
                    if (!byMaterial.TryGetValue(material, out List<CombineInstance> list))
                    { list = new List<CombineInstance>(); byMaterial.Add(material, list); }
                    list.Add(new CombineInstance
                    {
                        mesh = filter.sharedMesh, subMeshIndex = subMesh,
                        transform = Ship.worldToLocalMatrix * renderer.transform.localToWorldMatrix
                    });
                }
                sourceRenderers.Add(renderer);
            }
            foreach (var renderer in sourceRenderers) renderer.enabled = false;
            foreach (var group in byMaterial)
            {
                var mesh = new Mesh { name = "Batched ship surface - " + group.Key.name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(group.Value.ToArray(), true, true, false);
                mesh.RecalculateBounds(); generatedMeshes.Add(mesh);
                var combined = new GameObject("Batched " + group.Key.name);
                combined.transform.SetParent(Ship, false);
                combined.AddComponent<MeshFilter>().sharedMesh = mesh;
                combined.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
        }
        private void BuildHullPortholes()
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 7; i++)
                {
                    float z = -10.5f + i * 3.5f;
                    float x = side * (HullBeamAt(z) * .94f);
                    Vector3 center = new Vector3(x, -.62f, z);
                    Cylinder("Porthole brass rim", Ship, center, new Vector3(.58f, .10f, .58f), cream, Vector3.right * side);
                    Cylinder("Smoked porthole glass", Ship, center + Vector3.right * (side * .055f), new Vector3(.42f, .12f, .42f), black, Vector3.right * side);
                    Cylinder("Porthole inner glint", Ship, center + new Vector3(side * .12f, .07f, -.12f), new Vector3(.08f, .02f, .18f), teal);
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
            Vector3 deckHole = new Vector3(deckLadderPosition.x, MainDeckY + .1f, deckLadderPosition.z);
            deckHatchPivot = new GameObject("Deck hatch hinge").transform;
            deckHatchPivot.SetParent(Ship, false);
            deckHatchPivot.localPosition = deckHole + new Vector3(0, .03f, .92f);
            Box("Deck hatch lid", deckHatchPivot, new Vector3(0, 0, -.92f), new Vector3(1.6f, .12f, 1.85f), steel);
            Cylinder("Hatch wheel hub", deckHatchPivot, new Vector3(0, .12f, -.92f), new Vector3(.34f, .1f, .34f), amber);
            AddStation("Deck hatch control above", Station.DeckHatch, new Vector3(-1.05f, 1.22f, -3.5f), new Vector3(.48f, .8f, .5f), amber);
            AddStation("Deck hatch release below", Station.DeckHatch, new Vector3(-1.05f, -1.45f, -3.5f), new Vector3(.48f, .8f, .5f), amber);

            diveHatchPivot = new GameObject("Dive hatch hinge").transform;
            diveHatchPivot.SetParent(Ship, false);
            diveHatchPivot.localPosition = new Vector3(4.08f, -4.15f, 7.15f);
            Box("Watertight dive door", diveHatchPivot, new Vector3(0, 0, .9f), new Vector3(.16f, 2.2f, 2.0f), rust);
            Cylinder("Dive door wheel", diveHatchPivot, new Vector3(-.12f, 0, .9f), new Vector3(.56f, .1f, .56f), amber, Vector3.right);
            AddStation("Dive hatch control", Station.DiveHatch, new Vector3(3.62f, -4.25f, 8.9f), new Vector3(.5f, .8f, .5f), amber);
        }
        private void BuildLadder(string name, Vector3 position, float upperY, float lowerY, Station station)
        {
            float height = upperY - lowerY;
            float middleY = (upperY + lowerY) * .5f;
            Box(name + " mounting bulkhead", Ship, position + new Vector3(0, middleY, .17f),
                new Vector3(1.55f, height + .38f, .22f), teal);
            Cylinder(name + " port handrail", Ship, position + new Vector3(-.38f, middleY, 0), new Vector3(.09f, height * .5f, .09f), rust);
            Cylinder(name + " starboard handrail", Ship, position + new Vector3(.38f, middleY, 0), new Vector3(.09f, height * .5f, .09f), rust);
            for (int bracket = 0; bracket < 4; bracket++)
            {
                float y = Mathf.Lerp(lowerY + .2f, upperY - .2f, bracket / 3f);
                foreach (int side in new[] { -1, 1 })
                    Box(name + " wall anchor", Ship, position + new Vector3(side * .38f, y, .06f),
                        new Vector3(.14f, .1f, .22f), cream, false);
            }
            GameObject climbRail = new GameObject(name + " climb interaction");
            climbRail.transform.SetParent(Ship, false); climbRail.transform.localPosition = position + new Vector3(.38f, middleY, 0);
            climbRail.AddComponent<BoxCollider>().size = new Vector3(.3f, height, .3f);
            AddInteraction(climbRail, station);
            for (float y = lowerY + .18f; y < upperY; y += .24f)
                Cylinder(name + " round rung", Ship, position + new Vector3(0, y, 0), new Vector3(.08f, .72f, .08f), cream, Vector3.right);
        }
        private void BuildWheelhouse(Material amber)
        {
            const float centerZ = 2.35f, frontZ = -.45f, rearZ = 5.15f;
            const float floorY = 2.98f;
            // A broad lower deckhouse grows from the main deck. The starboard doorway
            // lines up with the stair landing, while both outer walkways stay clear.
            Box("Wheelhouse foundation bow wall", wheelhouseRoot, new Vector3(0, 1.92f, frontZ), new Vector3(5.9f, 2.12f, .42f), teal);
            Box("Wheelhouse foundation stern wall", wheelhouseRoot, new Vector3(0, 1.92f, rearZ), new Vector3(5.9f, 2.12f, .42f), teal);
            Box("Wheelhouse foundation port wall", wheelhouseRoot, new Vector3(-2.78f, 1.92f, centerZ), new Vector3(.38f, 2.12f, 5.25f), teal);
            Box("Wheelhouse foundation starboard forward wall", wheelhouseRoot, new Vector3(2.78f, 1.92f, 1.10f), new Vector3(.38f, 2.12f, 3.1f), teal);
            Box("Wheelhouse foundation starboard aft wall", wheelhouseRoot, new Vector3(2.78f, 1.92f, 4.70f), new Vector3(.38f, 2.12f, .9f), teal);
            Box("Wheelhouse deck floor", wheelhouseRoot, new Vector3(0, floorY, centerZ), new Vector3(5.55f, .2f, 5.75f), steel);

            // Fore windows face the vessel's bow (-Z); side panes are set between solid frames.
            for (int i = 0; i < 3; i++)
            {
                float x = -1.72f + i * 1.72f;
                Box("Bridge forward glass", wheelhouseRoot, new Vector3(x, 4.12f, frontZ + .06f), new Vector3(1.58f, 1.12f, .055f), black);
                Box("Forward window sill", wheelhouseRoot, new Vector3(x, 3.52f, frontZ), new Vector3(1.66f, .12f, .22f), cream);
                Box("Forward window mullion", wheelhouseRoot, new Vector3(x + .81f, 4.1f, frontZ), new Vector3(.09f, 1.2f, .16f), cream);
            }
            Box("Forward bridge brow", wheelhouseRoot, new Vector3(0, 4.74f, frontZ + .02f), new Vector3(6.0f, .16f, .3f), rust);
            Box("Forward bridge lower panel", wheelhouseRoot, new Vector3(0, 3.26f, frontZ), new Vector3(5.8f, .36f, .22f), cream);
            Box("Bridge aft wall", wheelhouseRoot, new Vector3(0, 4.08f, rearZ), new Vector3(5.8f, 2.05f, .2f), cream);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.82f;
                // The starboard gap is a direct entrance from the exterior stair.
                float[] panelCenters = side > 0 ? new[] { .42f, 1.15f, 4.9f } : new[] { .45f, 2.35f, 4.75f };
                float[] panelLengths = side > 0 ? new[] { 1.15f, 1.2f, .9f } : new[] { 1.1f, 1.05f, 1.1f };
                for (int i = 0; i < panelCenters.Length; i++)
                {
                    float z = panelCenters[i], length = panelLengths[i];
                    Box("Bridge side window glass", wheelhouseRoot, new Vector3(x, 4.15f, z), new Vector3(.055f, 1.02f, length), black);
                    Box("Bridge side window sill", wheelhouseRoot, new Vector3(x, 3.58f, z), new Vector3(.22f, .12f, length + .12f), cream);
                    Box("Bridge side window header", wheelhouseRoot, new Vector3(x, 4.72f, z), new Vector3(.22f, .12f, length + .12f), cream);
                    Box("Bridge side window frame fore", wheelhouseRoot, new Vector3(x, 4.15f, z - length * .5f), new Vector3(.22f, 1.15f, .09f), cream);
                    Box("Bridge side window frame aft", wheelhouseRoot, new Vector3(x, 4.15f, z + length * .5f), new Vector3(.22f, 1.15f, .09f), cream);
                }
                if (side < 0)
                    Box("Bridge side lower belt", wheelhouseRoot, new Vector3(x, 3.27f, centerZ), new Vector3(.22f, .35f, 5.7f), teal);
                else
                {
                    Box("Bridge side lower belt forward", wheelhouseRoot, new Vector3(x, 3.27f, 1.25f), new Vector3(.22f, .35f, 3.3f), teal);
                    Box("Bridge side lower belt aft", wheelhouseRoot, new Vector3(x, 3.27f, 4.75f), new Vector3(.22f, .35f, .85f), teal);
                    Box("Wheelhouse entrance front jamb", wheelhouseRoot, new Vector3(x, 3.95f, 3.00f), new Vector3(.24f, 1.55f, .16f), cream);
                    Box("Wheelhouse entrance rear jamb", wheelhouseRoot, new Vector3(x, 3.95f, 4.12f), new Vector3(.24f, 1.55f, .16f), cream);
                    Box("Wheelhouse entrance header", wheelhouseRoot, new Vector3(x, 4.73f, 3.56f), new Vector3(.24f, .14f, 1.28f), cream);
                }
                Box("Bridge side upper belt", wheelhouseRoot, new Vector3(x, 4.98f, centerZ), new Vector3(.22f, .24f, 5.7f), cream);
            }
            // Two shallow roof planes form a visible, weathered crown rather than a floating slab.
            var portRoof = Box("Bridge roof port slope", wheelhouseRoot, new Vector3(-1.48f, 5.2f, centerZ), new Vector3(3.05f, .22f, 6.2f), rust);
            portRoof.transform.localRotation = Quaternion.Euler(0, 0, -5f);
            var starboardRoof = Box("Bridge roof starboard slope", wheelhouseRoot, new Vector3(1.48f, 5.2f, centerZ), new Vector3(3.05f, .22f, 6.2f), rust);
            starboardRoof.transform.localRotation = Quaternion.Euler(0, 0, 5f);
            Box("Roof ridge", wheelhouseRoot, new Vector3(0, 5.33f, centerZ), new Vector3(.12f, .12f, 6.15f), cream);
            AddLamp(new Vector3(0, 4.8f, centerZ), amber, 6, 1.0f, wheelhouseRoot);
        }
        private void BuildWheelhouseConsole()
        {
            Box("Wheelhouse instrument console", wheelhouseRoot, new Vector3(0, 3.38f, .58f), new Vector3(4.45f, .56f, .74f), wood);
            Box("Console brass trim", wheelhouseRoot, new Vector3(0, 3.68f, .58f), new Vector3(4.3f, .045f, .7f), cream, false);
            for (int i = 0; i < 9; i++)
            {
                float x = -1.8f + i * .45f;
                Cylinder("Console instrument dial", wheelhouseRoot, new Vector3(x, 3.72f, .42f), new Vector3(.23f, .06f, .23f), black);
                Cylinder("Dial brass bezel", wheelhouseRoot, new Vector3(x, 3.755f, .42f), new Vector3(.16f, .025f, .16f), cream);
            }
            // Upright helm wheel is coaxial to the ship; its dark hub remains easy to read in game.
            Torus("Hand-fitted helm wheel", wheelhouseRoot, new Vector3(-.65f, 4.0f, .05f), .48f, .055f, rust, Vector3.forward);
            Cylinder("Helm wheel hub", wheelhouseRoot, new Vector3(-.65f, 4.0f, .05f), new Vector3(.3f, .11f, .3f), cream, Vector3.forward);
            for (int spoke = 0; spoke < 6; spoke++)
            {
                float a = spoke * Mathf.PI / 3f;
                Vector3 offset = new Vector3(Mathf.Cos(a) * .34f, Mathf.Sin(a) * .34f, 0);
                Cylinder("Helm wheel spoke", wheelhouseRoot, new Vector3(-.65f, 4.0f, .05f) + offset * .5f,
                    new Vector3(.055f, offset.magnitude, .055f), black, offset.sqrMagnitude > 0 ? offset : Vector3.right);
            }
        }
        private void BuildWheelhouseStairs()
        {
            const int steps = 14;
            Vector3 start = new Vector3(4.85f, MainDeckY + .02f, .45f);
            Vector3 end = new Vector3(3.55f, 2.92f, 3.65f);
            Vector3 along = end - start; Vector3 horizontal = new Vector3(along.x, 0, along.z).normalized;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + .5f) / steps;
                Vector3 point = Vector3.Lerp(start, end, t);
                var tread = Box("Bridge stair tread", Ship, point, new Vector3(.94f, .14f, .4f), rust);
                tread.transform.localRotation = Quaternion.Euler(0, Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg, 0);
                Box("Stair tread nosing", Ship, point + Vector3.up * .07f + horizontal * .13f, new Vector3(.98f, .045f, .035f), cream, false);
            }
            RailRun("Bridge stair handrail port", start + new Vector3(-.58f, .18f, -.12f), end + new Vector3(-.58f, .18f, -.12f), cream);
            RailRun("Bridge stair handrail starboard", start + new Vector3(.58f, .18f, .12f), end + new Vector3(.58f, .18f, .12f), cream);
            Box("Bridge stair landing", Ship, end + Vector3.down * .04f, new Vector3(1.45f, .18f, .9f), wood);
        }
        private void BuildDeckDetails()
        {
            // Bow anchor winch, chain guides and rounded bollards give the foredeck a working-boat silhouette.
            Box("Anchor winch base", Ship, new Vector3(0, 1.03f, -12.8f), new Vector3(1.9f, .28f, .9f), black);
            Cylinder("Anchor winch drum", Ship, new Vector3(0, 1.45f, -12.8f), new Vector3(.38f, 1.25f, .38f), rust, Vector3.right);
            Cylinder("Winch handwheel port", Ship, new Vector3(-.82f, 1.45f, -12.8f), new Vector3(.62f, .08f, .62f), cream, Vector3.forward);
            Cylinder("Winch handwheel starboard", Ship, new Vector3(.82f, 1.45f, -12.8f), new Vector3(.62f, .08f, .62f), cream, Vector3.forward);
            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float z in new[] { -11.5f, 12.8f })
                {
                    float x = side * Mathf.Max(1.1f, HullBeamAt(z) - .7f);
                    Cylinder("Mooring bollard stem", Ship, new Vector3(x, 1.17f, z), new Vector3(.22f, .35f, .22f), rust);
                    Cylinder("Mooring bollard cap", Ship, new Vector3(x, 1.53f, z), new Vector3(.38f, .12f, .38f), cream);
                    Cylinder("Life ring bracket", Ship, new Vector3(side * (HullBeamAt(z) - .1f), 1.45f, z), new Vector3(.08f, .25f, .08f), black);
                    Torus("Weathered orange life ring", Ship, new Vector3(side * (HullBeamAt(z) - .25f), 1.53f, z), .35f, .09f, safetyOrange, Vector3.right);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder("Wheelhouse exhaust stack", wheelhouseRoot, new Vector3(side * 1.55f, 5.83f, 5.45f), new Vector3(.34f, .92f, .34f), black);
                Cylinder("Exhaust stack collar", wheelhouseRoot, new Vector3(side * 1.55f, 6.35f, 5.45f), new Vector3(.48f, .12f, .48f), rust);
            }
            Cylinder("Wheelhouse aerial mast", wheelhouseRoot, new Vector3(0, 6.45f, 4.65f), new Vector3(.075f, 1.1f, .075f), steel);
            Cylinder("Wheelhouse radar dome", wheelhouseRoot, new Vector3(-1.9f, 5.85f, 2.3f), new Vector3(1.2f, .92f, 1.2f), cream);
            Cylinder("Wheelhouse radar base", wheelhouseRoot, new Vector3(-1.9f, 5.35f, 2.3f), new Vector3(.62f, .12f, .62f), steel);
            RailRun("Aerial crossbar", new Vector3(-.7f, 7.02f, 4.65f), new Vector3(.7f, 7.02f, 4.65f), cream);
            BuildHullWear();
        }
        private void BuildHullWear()
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 18; i++)
                {
                    float z = -13.2f + i * 1.52f;
                    if (side > 0 && z > 6.6f && z < 9.8f) continue;
                    float beam = HullBeamAt(z);
                    float length = .28f + (i % 4) * .12f;
                    float y = -1.05f - (i % 3) * .28f;
                    float x = side * beam * .94f;
                    Box("Paint-worn rust streak", Ship, new Vector3(x, y - length * .5f, z), new Vector3(.035f, length, .045f), rust, false);
                    for (int rivet = 0; rivet < 3; rivet++)
                        Cylinder("Hull plating rivet", Ship, new Vector3(side * (beam * .94f + .025f), -.95f, z - .23f + rivet * .23f),
                            new Vector3(.045f, .022f, .045f), cream, Vector3.right * side);
                }
        }
        private void BuildInterior(Material amber)
        {
            // Full-height lower decks are divided into readable rooms while keeping a clear central route.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = -10 + i * 6.2f;
                    Box("Middle deck locker", Ship, new Vector3(side * 4.45f, -1.48f, z), new Vector3(.9f, 1.25f, 1.35f), black);
                    Cylinder("Middle deck porthole rim", Ship, new Vector3(side * 5.48f, -1.05f, z + 2.2f), new Vector3(.62f, .1f, .62f), cream, Vector3.right * side);
                    Cylinder("Middle deck porthole lens", Ship, new Vector3(side * 5.43f, -1.05f, z + 2.2f), new Vector3(.42f, .1f, .42f), teal, Vector3.right * side);
                }
            }
            AddLamp(new Vector3(-3.8f, -1.45f, -7), amber, 6, .85f);
            AddLamp(new Vector3(3.8f, -1.45f, 0), amber, 6, .85f);
            AddLamp(new Vector3(-3.8f, -1.45f, 8), amber, 6, .85f);

            Box("Lower level engine mount", Ship, new Vector3(-3.5f, -4.92f, -8.3f), new Vector3(3.35f, .38f, 4.45f), rust);
            Cylinder("Main diesel flywheel", Ship, new Vector3(-3.55f, -4.12f, -8.25f), new Vector3(1.3f, .3f, 1.3f), cream, Vector3.forward);
            Cylinder("Diesel engine block", Ship, new Vector3(-3.5f, -4.22f, -8.3f), new Vector3(1.45f, 3.5f, 1.45f), black, Vector3.forward);
            Box("Engine service panel", Ship, new Vector3(-3.5f, -4.1f, -6.45f), new Vector3(2.25f, 1.45f, .12f), teal);
            for (int i = 0; i < 4; i++)
            {
                float x = -4.38f + i * .58f;
                Cylinder("Diesel cylinder head", Ship, new Vector3(x, -3.47f, -8.2f), new Vector3(.42f, .46f, .42f), rust);
                Cylinder("Diesel exhaust pipe", Ship, new Vector3(x, -3.05f, -8.2f), new Vector3(.16f, .62f, .16f), steel);
            }
            for (int i = 0; i < 3; i++)
                Cylinder("Engine fuel line", Ship, new Vector3(-1.75f, -4.15f + i * .4f, -8.4f), new Vector3(.1f, 1.05f, .1f), rust, Vector3.forward);
            AddStation("Engine fuel valve", Station.Engine, new Vector3(-1.85f, -4.38f, -6.1f), new Vector3(.85f, .9f, .8f), amber);

            // The center bulkhead separates the engine end from the diving compartment but leaves a wide doorway.
            Box("Lower compartment bulkhead port", Ship, new Vector3(-3.8f, -3.65f, 1.0f), new Vector3(3.2f, 2.85f, .2f), steel);
            Box("Lower compartment bulkhead starboard", Ship, new Vector3(3.8f, -3.65f, 1.0f), new Vector3(3.2f, 2.85f, .2f), steel);
            Box("Lower bulkhead lintel", Ship, new Vector3(0, -2.42f, 1.0f), new Vector3(2.0f, .35f, .2f), steel);
            // Diving chamber at the forward end; the door aligns to the hull opening on starboard.
            Box("Dive chamber port wall", Ship, new Vector3(1.45f, -3.65f, 10.1f), new Vector3(.2f, 2.85f, 4.4f), steel);
            Box("Dive chamber aft wall port", Ship, new Vector3(2.1f, -3.65f, 7.7f), new Vector3(1.3f, 2.85f, .2f), steel);
            Box("Dive chamber aft wall starboard", Ship, new Vector3(5.0f, -3.65f, 7.7f), new Vector3(1.0f, 2.85f, .2f), steel);
            Box("Dive chamber bow wall", Ship, new Vector3(3.5f, -3.65f, 12.45f), new Vector3(4.4f, 2.85f, .2f), steel);
            Box("Dive chamber overhead beam", Ship, new Vector3(3.5f, -2.34f, 10.0f), new Vector3(4.4f, .24f, 4.5f), steel);
            Cylinder("Dive chamber decompression tank", Ship, new Vector3(3.3f, -4.2f, 10.1f), new Vector3(.92f, 1.8f, .92f), cream);
            Cylinder("Dive tank valve", Ship, new Vector3(3.3f, -3.22f, 10.1f), new Vector3(.4f, .18f, .4f), rust);
            AddLamp(new Vector3(-3.8f, -4.5f, -8), amber, 6, 1.0f);
            AddLamp(new Vector3(4.3f, -4.5f, 10), amber, 6, 1.0f);
        }
        private void AddLamp(Vector3 localPosition, Material material, float range, float intensity, Transform parent = null)
        {
            Transform target = parent != null ? parent : Ship;
            Box("Warm utility lamp", target, localPosition, new Vector3(.18f, .24f, .18f), material, false);
            var lamp = new GameObject("Utility light").AddComponent<Light>();
            lamp.transform.SetParent(target, false); lamp.transform.localPosition = localPosition;
            lamp.type = LightType.Point; lamp.range = range; lamp.intensity = intensity; lamp.color = new Color(1, .72f, .42f);
        }
        private GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 axis = default)
        {
            if (axis == default) axis = Vector3.up;
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name; obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            obj.transform.localScale = size;
            if (axis.sqrMagnitude > .001f) obj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>(); if (collider != null) Object.Destroy(collider);
            return obj;
        }
        private void Torus(string name, Transform parent, Vector3 position, float radius, float tube, Material material, Vector3 normal)
        {
            const int ringSegments = 28, tubeSegments = 8;
            var vertices = new List<Vector3>((ringSegments + 1) * (tubeSegments + 1));
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(ringSegments * tubeSegments * 6);
            for (int ring = 0; ring <= ringSegments; ring++)
            {
                float a = ring / (float)ringSegments * Mathf.PI * 2;
                for (int tubeIndex = 0; tubeIndex <= tubeSegments; tubeIndex++)
                {
                    float b = tubeIndex / (float)tubeSegments * Mathf.PI * 2;
                    float radial = radius + Mathf.Cos(b) * tube;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radial, Mathf.Sin(b) * tube, Mathf.Sin(a) * radial));
                    uvs.Add(new Vector2(ring / (float)ringSegments, tubeIndex / (float)tubeSegments));
                    if (ring == ringSegments || tubeIndex == tubeSegments) continue;
                    int current = ring * (tubeSegments + 1) + tubeIndex;
                    int nextRing = current + tubeSegments + 1;
                    triangles.Add(current); triangles.Add(nextRing); triangles.Add(current + 1);
                    triangles.Add(current + 1); triangles.Add(nextRing); triangles.Add(nextRing + 1);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); generatedMeshes.Add(mesh);
            var ringObject = new GameObject(name); ringObject.transform.SetParent(parent, false);
            ringObject.transform.localPosition = position; ringObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            ringObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ringObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        private void RailRun(string name, Vector3 a, Vector3 b, Material material)
        {
            Vector3 delta = b - a;
            Cylinder(name + " upper rail", Ship, (a + b) * .5f, new Vector3(.08f, delta.magnitude * .5f, .08f), material, delta);
            Vector3 lowA = a - Vector3.up * .34f, lowB = b - Vector3.up * .34f;
            delta = lowB - lowA;
            Cylinder(name + " lower rail", Ship, (lowA + lowB) * .5f, new Vector3(.055f, delta.magnitude * .5f, .055f), material, delta);
            int posts = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.15f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)posts) - Vector3.up * .23f;
                Cylinder(name + " stanchion", Ship, p, new Vector3(.07f, .48f, .07f), material);
            }
        }
        private void BuildPerimeterRails()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                const int sections = 48;
                Vector3 previousTop = Vector3.zero, previousLower = Vector3.zero;
                for (int i = 0; i <= sections; i++)
                {
                    float z = Mathf.Lerp(-14.45f, 14.45f, i / (float)sections);
                    float x = side * Mathf.Max(1.1f, HullBeamAt(z) - .2f);
                    Vector3 top = new Vector3(x, 1.72f, z), lower = new Vector3(x, 1.34f, z);
                    if (i > 0)
                    {
                        Vector3 topDelta = top - previousTop, lowerDelta = lower - previousLower;
                        Cylinder("Continuous upper guard rail", Ship, (top + previousTop) * .5f,
                            new Vector3(.07f, topDelta.magnitude * .53f, .07f), rust, topDelta);
                        Cylinder("Continuous lower guard rail", Ship, (lower + previousLower) * .5f,
                            new Vector3(.05f, lowerDelta.magnitude * .53f, .05f), cream, lowerDelta);
                    }
                    if (i % 3 == 0)
                    {
                        Cylinder("Guard rail stanchion", Ship, new Vector3(x, 1.46f, z), new Vector3(.075f, .44f, .075f), rust);
                        var boundary = Box("Guard rail collision", Ship, new Vector3(x, 1.38f, z), new Vector3(.12f, .9f, .12f), steel);
                        var renderer = boundary.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false;
                    }
                    previousTop = top; previousLower = lower;
                }
            }
            float bowBeam = HullBeamAt(-14.5f) - .15f, sternBeam = HullBeamAt(14.5f) - .15f;
            RailRun("Bow guard rail", new Vector3(-bowBeam, 1.72f, -14.5f), new Vector3(bowBeam, 1.72f, -14.5f), rust);
            RailRun("Stern guard rail", new Vector3(-sternBeam, 1.72f, 14.5f), new Vector3(sternBeam, 1.72f, 14.5f), rust);
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
            if (particleShader != null) { renderer.sharedMaterial = new Material(particleShader); materials.Add(renderer.sharedMaterial); }
            stormRain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void CreateShipWake()
        {
            var wakeObject = new GameObject("Ship wake foam", typeof(ParticleSystem));
            shipWake = wakeObject.GetComponent<ParticleSystem>();
            var main = shipWake.main;
            main.loop = true; main.duration = 1; main.startLifetime = 2.4f; main.startSpeed = .32f;
            main.startSize = .28f; main.startColor = new Color(.64f, .9f, .94f, .62f);
            main.maxParticles = 180; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = shipWake.emission; emission.rateOverTime = 0;
            var shape = shipWake.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14; shape.radius = .55f; shape.length = 1.2f;
            var renderer = wakeObject.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader != null)
            {
                var wakeMaterial = new Material(particleShader);
                if (wakeMaterial.HasProperty("_BaseColor")) wakeMaterial.SetColor("_BaseColor", Color.white);
                if (wakeMaterial.HasProperty("_Color")) wakeMaterial.SetColor("_Color", Color.white);
                renderer.sharedMaterial = wakeMaterial; materials.Add(wakeMaterial);
            }
            shipWake.Play();
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
                bool descending = currentY > -.5f;
                destination = new Vector3(deckLadderPosition.x + 1.4f, descending ? MiddleDeckY + .23f : MainDeckY + .25f, deckLadderPosition.z);
                return true;
            }
            if (station == Station.LadderMiddleLower)
            {
                bool descending = currentY > -3.5f;
                destination = new Vector3(lowerLadderPosition.x + 1.4f, descending ? LowerDeckY + .23f : MiddleDeckY + .23f, lowerLadderPosition.z);
                return true;
            }
            destination = Vector3.zero;
            return false;
        }
        private Material MakeMaterial(Material template, Color color)
        {
            Material material = new Material(template); material.color = color; materials.Add(material);
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            { name = "Procedural aged surface", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            var pixels = new Color32[128 * 128];
            int seed = materials.Count * 173 + 29;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float broad = Mathf.PerlinNoise((x + seed) * .025f, (y - seed) * .025f);
                    float fine = Mathf.PerlinNoise((x + seed * 3) * .085f, (y + seed) * .085f);
                    float value = Mathf.Clamp(.96f + broad * .045f + fine * .012f, .94f, 1.025f);
                    byte shade = (byte)Mathf.RoundToInt(value * 255f);
                    pixels[y * 128 + x] = new Color32(shade, shade, shade, 255);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true); generatedTextures.Add(texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .08f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .32f);
            return material;
        }
        private Mesh GetRoundedBoxMesh()
        {
            if (roundedBoxMesh != null) return roundedBoxMesh;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var triangles = new List<int>();
            AddRoundedBoxFace(vertices, normals, uvs, triangles, Vector3.forward, Vector3.right, Vector3.up);
            AddRoundedBoxFace(vertices, normals, uvs, triangles, Vector3.back, Vector3.up, Vector3.right);
            AddRoundedBoxFace(vertices, normals, uvs, triangles, Vector3.right, Vector3.up, Vector3.forward);
            AddRoundedBoxFace(vertices, normals, uvs, triangles, Vector3.left, Vector3.forward, Vector3.up);
            AddRoundedBoxFace(vertices, normals, uvs, triangles, Vector3.up, Vector3.forward, Vector3.right);
            AddRoundedBoxFace(vertices, normals, uvs, triangles, Vector3.down, Vector3.right, Vector3.forward);
            roundedBoxMesh = new Mesh { name = "Soft rounded game prop" };
            roundedBoxMesh.SetVertices(vertices); roundedBoxMesh.SetNormals(normals);
            roundedBoxMesh.SetUVs(0, uvs); roundedBoxMesh.SetTriangles(triangles, 0);
            roundedBoxMesh.RecalculateBounds(); generatedMeshes.Add(roundedBoxMesh);
            return roundedBoxMesh;
        }
        private static void AddRoundedBoxFace(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 face, Vector3 axisU, Vector3 axisV)
        {
            const int divisions = 4; const float bevel = .12f;
            int start = vertices.Count, side = divisions + 1;
            for (int y = 0; y <= divisions; y++)
                for (int x = 0; x <= divisions; x++)
                {
                    float u = x / (float)divisions, v = y / (float)divisions;
                    Vector3 raw = face * .5f + axisU * (u - .5f) + axisV * (v - .5f);
                    Vector3 core = new Vector3(
                        Mathf.Clamp(raw.x, -.5f + bevel, .5f - bevel),
                        Mathf.Clamp(raw.y, -.5f + bevel, .5f - bevel),
                        Mathf.Clamp(raw.z, -.5f + bevel, .5f - bevel));
                    Vector3 delta = raw - core;
                    Vector3 normal = delta.sqrMagnitude > .000001f ? delta.normalized : face;
                    vertices.Add(core + normal * bevel); normals.Add(normal);
                    uvs.Add(new Vector2(u * 3f, v * 3f));
                    if (x == divisions || y == divisions) continue;
                    int a = start + y * side + x, b = a + 1, d = a + side, c = d + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }
        }
        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.AddComponent<MeshFilter>().sharedMesh = GetRoundedBoxMesh();
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) obj.AddComponent<BoxCollider>();
            return obj;
        }
        private DriftInteractable AddStation(string name, Station station, Vector3 position, Vector3 scale, Material material, Item item = Item.None, bool dropped = false, Transform parent = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent != null ? parent : Ship, false); obj.transform.localPosition = position;
            var hitbox = obj.AddComponent<BoxCollider>(); hitbox.size = scale;
            BuildStationVisual(obj.transform, station, scale, material, item);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Objective Marker"; marker.transform.SetParent(obj.transform, false); marker.transform.localPosition = Vector3.up * (scale.y * .5f + .55f);
            marker.transform.localScale = Vector3.one * .13f; marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            marker.GetComponent<Collider>().enabled = false; Object.Destroy(marker.GetComponent<Collider>()); marker.SetActive(false);
            var interaction = obj.AddComponent<DriftInteractable>(); interaction.Initialize(station, item, dropped, marker);
            if (dropped) drops.Add(interaction); else stations.Add(interaction);
            return interaction;
        }
        private void BuildStationVisual(Transform parent, Station station, Vector3 bounds, Material material, Item item)
        {
            switch (station)
            {
                case Station.Note:
                    Box("Brass-edged notice board", parent, Vector3.zero, new Vector3(.12f, .88f, .9f), wood, false);
                    Box("Folded work sheet", parent, new Vector3(.075f, 0, 0), new Vector3(.025f, .68f, .58f), cream, false);
                    for (int i = 0; i < 4; i++) Box("Ink line", parent, new Vector3(.093f, .18f - i * .12f, 0), new Vector3(.01f, .018f, .39f - (i % 2) * .08f), black, false);
                    break;
                case Station.Barrel:
                    Cylinder("Rainwater barrel", parent, new Vector3(0, -.05f, 0), new Vector3(bounds.x, bounds.y * .48f, bounds.z), steel);
                    Cylinder("Barrel top lid", parent, new Vector3(0, bounds.y * .48f, 0), new Vector3(bounds.x * .96f, .055f, bounds.z * .96f), rust);
                    for (int i = 0; i < 3; i++) Cylinder("Barrel hoop", parent, new Vector3(0, -.35f + i * .3f, 0), new Vector3(bounds.x * 1.01f, .035f, bounds.z * 1.01f), cream);
                    break;
                case Station.Purifier:
                    Box("Purifier cabinet", parent, new Vector3(0, -.04f, 0), new Vector3(bounds.x * .78f, bounds.y * .82f, bounds.z * .76f), cream, false);
                    Cylinder("Purifier filter canister", parent, new Vector3(-.33f, -.03f, -.03f), new Vector3(.38f, bounds.y * .75f, .38f), teal);
                    Cylinder("Filter cap", parent, new Vector3(-.33f, bounds.y * .36f, -.03f), new Vector3(.42f, .12f, .42f), rust);
                    Cylinder("Pressure gauge", parent, new Vector3(.24f, .22f, -.4f), new Vector3(.28f, .12f, .28f), black, Vector3.forward);
                    Cylinder("Purifier faucet", parent, new Vector3(.31f, -.28f, -.38f), new Vector3(.12f, .45f, .12f), steel);
                    Box("Purifier status lamp", parent, new Vector3(.25f, .08f, -.4f), new Vector3(.12f, .12f, .035f), teal, false);
                    break;
                case Station.Engine:
                    Box("Engine control plinth", parent, new Vector3(0, -.18f, 0), new Vector3(.72f, .48f, .64f), black, false);
                    Cylinder("Fuel valve stem", parent, new Vector3(0, .1f, -.12f), new Vector3(.1f, .55f, .1f), rust);
                    Cylinder("Fuel shutoff wheel", parent, new Vector3(0, .4f, -.12f), new Vector3(.56f, .08f, .56f), cream, Vector3.forward);
                    Cylinder("Fuel pressure dial", parent, new Vector3(.24f, .15f, -.34f), new Vector3(.24f, .08f, .24f), cream, Vector3.forward);
                    break;
                case Station.DeckRepair:
                    Box("Damaged deck plate", parent, Vector3.zero, new Vector3(bounds.x, .08f, bounds.z), rust, false);
                    Box("Replacement patch plate", parent, new Vector3(0, .055f, 0), new Vector3(bounds.x * .45f, .035f, bounds.z * .52f), steel, false);
                    for (int i = 0; i < 4; i++) Cylinder("Repair plate rivet", parent,
                        new Vector3((i % 2 == 0 ? -1 : 1) * bounds.x * .37f, .08f, (i < 2 ? -.34f : .34f) * bounds.z),
                        new Vector3(.075f, .04f, .075f), cream);
                    break;
                case Station.Radio:
                    Box("Radio receiver", parent, Vector3.zero, new Vector3(bounds.x, bounds.y * .8f, bounds.z), black, false);
                    Box("Radio signal window", parent, new Vector3(0, .1f, -.27f), new Vector3(.48f, .13f, .025f), teal, false);
                    for (int i = 0; i < 3; i++) Cylinder("Radio selector", parent, new Vector3(-.22f + i * .22f, -.14f, -.28f), new Vector3(.09f, .08f, .09f), cream);
                    break;
                case Station.Helm:
                    // The wheel and console are built together so their centerlines stay aligned.
                    break;
                case Station.DeckHatch:
                    Box("Hatch control pedestal", parent, new Vector3(0, -.2f, 0), new Vector3(.38f, .5f, .32f), steel, false);
                    Cylinder("Hatch release wheel", parent, new Vector3(0, .18f, -.12f), new Vector3(.46f, .07f, .46f), material, Vector3.forward);
                    break;
                case Station.DiveHatch:
                    Box("Dive hatch control box", parent, new Vector3(0, -.16f, 0), new Vector3(.42f, .48f, .32f), black, false);
                    Cylinder("Dive hatch locking wheel", parent, new Vector3(-.06f, .1f, -.15f), new Vector3(.48f, .07f, .48f), cream, Vector3.right);
                    break;
                case Station.Fishing:
                    Cylinder("Fishing line post", parent, new Vector3(0, .12f, 0), new Vector3(.12f, .42f, .12f), rust);
                    Cylinder("Fishing line guide", parent, new Vector3(0, .36f, 0), new Vector3(.14f, .035f, .14f), cream);
                    break;
                case Station.Pickup:
                    if (item == Item.FishingRod)
                    {
                        Cylinder("Fishing rod shaft", parent, Vector3.zero, new Vector3(.035f, .72f, .035f), wood, Vector3.forward);
                        Cylinder("Fishing rod grip", parent, new Vector3(0, 0, -.5f), new Vector3(.08f, .24f, .08f), black, Vector3.forward);
                    }
                    else if (item == Item.Fuel)
                    {
                        Cylinder("Fuel canister", parent, Vector3.zero, new Vector3(.28f, .42f, .28f), rust);
                        Cylinder("Fuel canister neck", parent, new Vector3(0, .27f, 0), new Vector3(.12f, .12f, .12f), cream);
                    }
                    else if (item == Item.PurifierPart)
                    {
                        Cylinder("Floating filter cartridge", parent, Vector3.zero, new Vector3(.16f, .4f, .16f), cream, Vector3.forward);
                        Cylinder("Filter end band", parent, new Vector3(0, 0, .2f), new Vector3(.2f, .08f, .2f), teal, Vector3.forward);
                    }
                    else Box("Supply pickup", parent, Vector3.zero, new Vector3(.3f, .28f, .3f), material, false);
                    break;
            }
        }
        public bool CanDrop => drops.Count < 64;
        public float WaterSurfaceY => root.TransformPoint(new Vector3(0, WaterSurfaceLocalY, 0)).y;
        public bool IsOutsideHull(Vector3 worldPosition)
        {
            Vector3 local = Ship.InverseTransformPoint(worldPosition);
            float z = Mathf.Clamp(local.z, -15.1f, 15.1f);
            bool beyondHull = local.z < -15.1f || local.z > 15.1f || Mathf.Abs(local.x) > HullBeamAt(z) + .2f;
            bool outsideDiveDoor = diveHatchOpen && local.y < -2.8f && local.x > 4.2f && local.z > 6.6f && local.z < 10.1f;
            return beyondHull || outsideDiveDoor;
        }
        public bool IsInWater(Vector3 worldPosition) => IsOutsideHull(worldPosition) && worldPosition.y < WaterSurfaceY + .35f;
        public bool IsUnderwater(Vector3 worldPosition) => IsOutsideHull(worldPosition) && worldPosition.y < WaterSurfaceY - .05f;
        private float WaterSurfaceShipLocalY
        {
            get
            {
                Vector3 point = Ship.position; point.y = WaterSurfaceY;
                return Ship.InverseTransformPoint(point).y;
            }
        }
        public bool IsExposed(Vector3 worldPosition) => Ship.InverseTransformPoint(worldPosition).y > .3f && !InsideWheelhouse(worldPosition);
        public void SpawnPurifierPartAtSea()
        {
            float z = Random.Range(8.1f, 9.0f);
            AddPickup(Item.PurifierPart, new Vector3(HullBeamAt(z) + 1.2f, WaterSurfaceShipLocalY, z), true);
        }
        public void SetStorm(bool active)
        {
            stormActive = active;
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
            shipSpeed = 0;
            Ship.localPosition = new Vector3(0, ShipRestY, 0);
            Ship.localRotation = Quaternion.identity;
            UpdateOceanSurface();
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
        public void Steer(float rudder, float throttle, float dt)
        {
            float targetSpeed = Mathf.Clamp(throttle, -1, 1) * 3.6f;
            float acceleration = Mathf.Abs(targetSpeed) > Mathf.Abs(shipSpeed) ? .9f : 1.35f;
            shipSpeed = Mathf.MoveTowards(shipSpeed, targetSpeed, acceleration * dt);
            float steerAuthority = Mathf.Clamp01(Mathf.Abs(shipSpeed) / 1.4f);
            Ship.Rotate(Vector3.up, rudder * 12f * steerAuthority * Mathf.Sign(shipSpeed) * dt, Space.World);
            if (Mathf.Abs(shipSpeed) > .001f) Ship.Translate(Vector3.back * shipSpeed * dt, Space.Self);
        }
        public float SpeedKnots => Mathf.Abs(shipSpeed) * 1.94384f;
        public void StartGullStrike() { gullFlight = 0; Gull.gameObject.SetActive(true); }
        public void Tick(float dt)
        {
            waterTime += dt;
            Vector3 shipPosition = Ship.localPosition;
            float floatAmplitude = stormActive ? .12f : .055f;
            float waveRate = stormActive ? 1.8f : 1.1f;
            shipPosition.y = ShipRestY + Mathf.Sin(waterTime * waveRate) * floatAmplitude;
            Ship.localPosition = shipPosition;
            Vector3 angles = Ship.localEulerAngles;
            float roll = Mathf.Sin(waterTime * waveRate * .72f + .8f) * (stormActive ? 2.4f : .85f);
            float pitch = Mathf.Sin(waterTime * waveRate * .9f) * (stormActive ? 1.7f : .6f);
            Ship.localRotation = Quaternion.Euler(pitch, angles.y, roll);
            if (shipWake != null)
            {
                shipWake.transform.position = Ship.TransformPoint(new Vector3(0, WaterSurfaceShipLocalY, 15.25f));
                shipWake.transform.rotation = Ship.rotation;
                var emission = shipWake.emission;
                emission.rateOverTime = Mathf.Clamp(Mathf.Abs(shipSpeed) * 3.2f, 0, 18);
            }
            UpdateOceanSurface();
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
            return local.y > 2.0f && Mathf.Abs(local.x) < 3.35f && local.z > -1.0f && local.z < 5.4f;
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
                if (station.Kind == Station.DeckRepair)
                {
                    var repairSurface = station.GetComponentInChildren<Renderer>(true);
                    if (repairSurface != null) repairSurface.sharedMaterial = state.IsDone(TaskId.Deck) ? steel : rust;
                }
            }
        }
        public void Dispose()
        {
            foreach (var mesh in generatedMeshes) if (mesh != null) Object.Destroy(mesh);
            generatedMeshes.Clear();
            foreach (var material in materials) Object.Destroy(material);
            materials.Clear();
            foreach (var texture in generatedTextures) Object.Destroy(texture);
            generatedTextures.Clear();
        }
    }
}

