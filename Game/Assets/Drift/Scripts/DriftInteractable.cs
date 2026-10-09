using UnityEngine;

namespace Drift
{
    public enum Station
    {
        Note, Fishing, Barrel, Purifier, Engine, DeckRepair, Radio, Helm, Pickup,
        DeckHatch, LadderDeckMiddle, LadderMiddleLower, DiveHatch
    }
    public sealed class DriftInteractable : MonoBehaviour
    {
        public Station Kind { get; private set; }
        public Item PickupItem { get; private set; }
        public bool Dropped { get; private set; }
        private GameObject marker;
        public void Initialize(Station kind, Item item, bool dropped, GameObject objectiveMarker)
        {
            Kind = kind; PickupItem = item; Dropped = dropped; marker = objectiveMarker;
        }
        public void ShowMarker(bool visible)
        {
            if (marker != null && marker.activeSelf != visible) marker.SetActive(visible);
        }
    }
}
