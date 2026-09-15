using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LucidLoop.Gyms.Tests
{
    public class DialogueWaveformTests
    {
        static byte[] Pcm(int samples){var data=new byte[samples*2];for(int i=0;i<samples;i++){short v=(short)(Mathf.Sin(i*.1f)*12000);data[i*2]=(byte)v;data[i*2+1]=(byte)(v>>8);}return data;}
        [Test] public void QueuedPcmDoesNotAnimateUntilConsumed()
        {var meter=new NpcSpeechMeter();meter.Begin(1);meter.Push(Pcm(2400),1);Assert.AreEqual(0,meter.Level);meter.Advance(1,1200,false);Assert.Greater(meter.Level,.1f);meter.Advance(1,1200,false);Assert.AreEqual(0,meter.Level);}
        [Test] public void StaleGenerationAndEndResetDoNotAnimate()
        {var meter=new NpcSpeechMeter();meter.Begin(2);meter.Push(Pcm(2000),1);meter.Advance(1,2000,false);Assert.AreEqual(0,meter.Level);meter.Push(Pcm(2000),2);meter.Advance(2,1000,false);Assert.Greater(meter.Level,0);meter.Advance(2,1000,true);Assert.AreEqual(0,meter.Level);}
        [UnityTest] public IEnumerator PanelCloseRejectsLateTextAndResetsWave()
        {
            var go=new GameObject("Dialogue test");var box=go.AddComponent<DialogueBoxController>();box.Open("Theo","The Socialite",false);int old=box.Epoch;
            box.SetResponse("Current response",old);box.Meter.Begin(1);box.Meter.Push(Pcm(2000),1);box.Meter.Advance(1,1000,false);yield return null;
            Assert.IsFalse(box.MicrophoneAvailable);Assert.Greater(box.Waveform.VisibleLevel,0);
            box.Close();Assert.AreEqual(0,box.Waveform.VisibleLevel);Assert.AreEqual(0,box.Meter.Level);
            box.SetResponse("Late",old);Assert.AreEqual(DialogueBoxState.Closed,box.State);
            box.Open("Maya","Close friend",true);Assert.AreNotEqual(old,box.Epoch);Assert.AreEqual("",box.Reply.text);
            Object.Destroy(go);yield return null;
        }
        [UnityTest] public IEnumerator PlayerTranscriptSurvivesNpcReplyAndRejectsClosedConversation()
        {
            var go=new GameObject("Player transcript test");var box=go.AddComponent<DialogueBoxController>();
            box.Open("Maya","Close friend",true);int epoch=box.Epoch;
            box.SetPlayerTranscript("Please wait",epoch);
            box.SetPlayerTranscript("Please wait here.",epoch);
            box.SetResponse("Of course. I'll wait.",epoch);box.SetStatus("Speaking...");
            yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreEqual("Please wait here.",box.PlayerTranscript);
            var texts=go.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
            var player=System.Array.Find(texts,t=>t.name=="Latest player transcript");
            var npc=System.Array.Find(texts,t=>t.name=="NPC response");
            Assert.AreEqual("You said: Please wait here.",player.text);
            var a=new Vector3[4];var b=new Vector3[4];
            player.transform.parent.GetComponent<RectTransform>().GetWorldCorners(a);
            npc.transform.parent.parent.GetComponent<RectTransform>().GetWorldCorners(b);
            Assert.GreaterOrEqual(a[0].y,b[2].y);
            box.Close();box.Open("Theo","The Socialite",true);
            box.SetPlayerTranscript("Late old words",epoch);Assert.AreEqual("",box.PlayerTranscript);
            box.Reply.text="Hello Theo";box.Submit();Assert.AreEqual("Hello Theo",box.PlayerTranscript);
            Object.Destroy(go);yield return null;
        }
        [UnityTest] public IEnumerator ReplyAndWaveformHaveSeparateBounds()
        {
            var go=new GameObject("Dialogue bounds");var box=go.AddComponent<DialogueBoxController>();box.Open("Theo","The Socialite",true);yield return null;Canvas.ForceUpdateCanvases();
            var a=new Vector3[4];var b=new Vector3[4];box.Reply.textViewport.GetWorldCorners(a);box.Waveform.rectTransform.GetWorldCorners(b);Assert.LessOrEqual(a[2].x,b[0].x);
            string sent=null;box.Submitted+=value=>sent=value;box.Reply.text="Hello";box.Submit();Assert.AreEqual("Hello",sent);Assert.AreEqual(0,box.Meter.Level);
            Object.Destroy(go);yield return null;
        }
    }
}
