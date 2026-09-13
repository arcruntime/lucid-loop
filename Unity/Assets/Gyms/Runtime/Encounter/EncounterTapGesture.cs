using UnityEngine;

namespace LucidLoop.Gyms
{
    // Tracks a single pointer; leaving the tap radius permanently cancels this gesture.
    public sealed class EncounterTapGesture
    {
        int pointerId;
        Vector2 origin;
        float radius;
        bool active;
        public void Cancel() => active = false;
        public void Begin(int id, Vector2 position, float tolerance, bool overUi)
        { pointerId = id; origin = position; radius = Mathf.Max(1, tolerance); active = !overUi; }
        public void Move(int id, Vector2 position)
        { if (active && (id != pointerId || (position - origin).sqrMagnitude > radius * radius)) Cancel(); }
        public bool End(int id, Vector2 position, bool overUi)
        {
            Move(id, position);
            bool accepted = active && !overUi;
            Cancel();
            return accepted;
        }
    }
}
