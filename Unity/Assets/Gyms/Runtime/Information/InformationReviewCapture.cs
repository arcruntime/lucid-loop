#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class InformationReviewCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            var ui=GetComponent<InformationNotificationUI>();
            yield return Save("01-unread");ui.ClickIcon();yield return Save("02-banner");
            ui.ClickBanner();yield return Save("03-panel");ui.Panel.Close();yield return Save("04-read");
            Debug.Log("INFORMATION_CAPTURE_OK");Destroy(this);
        }
        IEnumerator Save(string stage)
        {
            yield return new WaitForSecondsRealtime(3f);yield return new WaitForEndOfFrame();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/information/"+Screen.width+"x"+Screen.height));Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,stage+".png"));

        }
    }
}
#endif
