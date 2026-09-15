#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class VictoryReviewCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3);
            yield return new WaitForEndOfFrame();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/victory"));
            Directory.CreateDirectory(folder);
            string file=Path.Combine(folder,"victory-"+Screen.width+"x"+Screen.height+".png");
            ScreenCapture.CaptureScreenshot(file); Debug.Log("VICTORY_CAPTURE: "+file); Destroy(this);
        }
    }
}
#endif
