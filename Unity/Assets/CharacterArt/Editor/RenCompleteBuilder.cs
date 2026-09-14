using UnityEditor;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenCompleteBuilder
    {
        [MenuItem("Lucid Loop/Ren LOD0/Rebuild assembled character")]
        public static void Build()
        {
            RenLOD0Builder.Build();
            RenLOD0PrefabBuilder.Build();
            RenGuardedBuilder.Build();
            RenLOD1Builder.Build();
        }
    }
}
