using OsuFramework.Unity.Allocation;
using osu.Framework.Allocation;
using UnityEngine;

namespace LucidLoop.Gyms
{
    public sealed class GymSession
    {
        public CharacterActor Conversation;
        public CharacterActor Approaching;
        public string Notice = "Tap the floor to walk. Hold a character to talk.";
        public bool IsTalking => Conversation != null;
    }

    public partial class GymRoot : DependencyNodeBehaviour
    {
        [Cached] public GymSession Session { get; } = new GymSession();
        protected override void Awake()
        {
            Application.targetFrameRate = 30;
            base.Awake();
        }
    }
}
