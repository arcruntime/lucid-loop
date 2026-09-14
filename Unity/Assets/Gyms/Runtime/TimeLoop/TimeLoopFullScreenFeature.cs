using UnityEngine.Rendering.Universal;
namespace LucidLoop.Gyms
{
    // Uses URP 17's supported Full Screen Pass implementation, including Render Graph.
    public sealed class TimeLoopFullScreenFeature : FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
        {
            var controller=TimeLoopTransitionController.Active;
            if(!controller || !controller.IsTransitioning || renderingData.cameraData.camera!=controller.GameplayCamera)return;
            passMaterial=controller.EffectMaterial;
            base.AddRenderPasses(renderer,ref renderingData);
        }
    }
}
