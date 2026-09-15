#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class DialogueSpeechReview : MonoBehaviour
    {
        IEnumerator Start()
        {
            var box=GameObject.Find("Offline dialogue preview").GetComponent<DialogueBoxController>();
            yield return new WaitForSecondsRealtime(1);
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/dialogue-waveform-captures"));Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,$"{Screen.width}x{Screen.height}-idle.png"));
            box.PlayTest();float max=0,until=Time.unscaledTime+6;bool captured=false;
            while(Time.unscaledTime<until)
            {
                max=Mathf.Max(max,box.Waveform.VisibleLevel);
                if(!captured && box.Waveform.VisibleLevel>.1f){captured=true;ScreenCapture.CaptureScreenshot(Path.Combine(dir,$"{Screen.width}x{Screen.height}-speaking.png"));}
                yield return null;
            }
            if(max<.05f)throw new Exception("Assigned speech did not drive the waveform from AudioSource output");
            if(box.Waveform.VisibleLevel>.03f)throw new Exception("Waveform did not settle after actual speech ended");
            box.Close();if(box.Waveform.VisibleLevel!=0)throw new Exception("Close did not immediately reset speech");
            Debug.Log("DIALOGUE_SPEECH_OK: dedicated speech clip produced measured output; waveform followed and settled; close reset immediately.");
            Destroy(gameObject);
        }
    }
}
#endif
