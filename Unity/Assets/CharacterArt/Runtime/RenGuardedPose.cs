using System;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    public sealed class RenGuardedPose : ScriptableObject
    {
        [Serializable] public struct Bone
        {
            public string Path;
            public Vector3 RestPosition, Position;
            public Quaternion RestRotation, Rotation;
        }
        public Bone[] Bones = Array.Empty<Bone>();
        public float EnterSeconds = 1.2f, ExitSeconds = 1.4f;
    }
}
