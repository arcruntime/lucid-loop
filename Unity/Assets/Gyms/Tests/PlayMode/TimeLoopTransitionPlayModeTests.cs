using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class DelayedLoopTestAdapter : LoopResetAdapter
    {
        public bool Release,Fail;
        public int Calls,Ends,Number=1;
        public override bool CanTrigger=>true;
        public override int LoopNumber=>Number;
        public override IEnumerator RestoreCheckpoint(LoopResetResult result)
        {Calls++;while(!Release)yield return null;if(Fail){result.Error="test checkpoint failure";yield break;}result.Success=true;result.LoopNumber=++Number;}
        public override void EndFreeze(bool success){Ends++;}
    }
    public sealed class TimeLoopTestWriter : MonoBehaviour {}
    public sealed class TimeLoopTransitionPlayModeTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        float oldScale;
        GameObject New(string name){var go=new GameObject(name);objects.Add(go);return go;}
        [SetUp] public void Setup(){if(TimeLoopTransitionController.Active)TimeLoopTransitionController.Active.CancelTransition();oldScale=Time.timeScale;}
        [TearDown] public void Cleanup(){Time.timeScale=oldScale;foreach(var go in objects)if(go)UnityEngine.Object.DestroyImmediate(go);objects.Clear();}
        TimeLoopTransitionController Controller(LoopResetAdapter adapter,LoopRewindActor actor=null)
        {
            var c=New("Transition test").AddComponent<TimeLoopTransitionController>();
            c.EnableDebugInput=false;c.GameplayCamera=New("Test camera").AddComponent<Camera>();c.ResetAdapter=adapter;
            if(actor)c.Actors=new[]{actor};foreach(var stage in c.Stages)stage.Duration=.035f;return c;
        }
        static IEnumerator Finished(TimeLoopTransitionController c)
        {float end=Time.realtimeSinceStartup+5;while(c.IsTransitioning){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end),c.LastError);yield return null;}}
        [Test]
        public void BoundedHistoryInterpolatesPathsAndPreservesSpriteFrames()
        {
            var actor=New("Recorded actor").AddComponent<LoopRewindActor>();actor.SampleRate=20;actor.HistorySeconds=.1f;
            var renderer=actor.gameObject.AddComponent<SpriteRenderer>();
            var texture=new Texture2D(2,2);var a=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.zero);var b=Sprite.Create(texture,new Rect(1,0,1,1),Vector2.zero);
            try
            {
                actor.Sprites=new[]{renderer};actor.Initialize();
                for(int i=0;i<6;i++){actor.transform.position=new Vector3(i*2,0,0);renderer.sprite=i<5?a:b;actor.Record(i);}
                Assert.That(actor.SampleCount,Is.EqualTo(3));
                actor.Replay(.25f);Assert.That(actor.transform.position.x,Is.EqualTo(9).Within(.001));Assert.That(renderer.sprite,Is.SameAs(a));
                actor.Replay(0);Assert.That(actor.transform.position.x,Is.EqualTo(10));Assert.That(renderer.sprite,Is.SameAs(b));
                actor.Replay(1);Assert.That(actor.transform.position.x,Is.EqualTo(6));Assert.That(renderer.sprite,Is.SameAs(a));
            }
            finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(texture);}
        }
        [UnityTest]
        public IEnumerator RepeatedResetCommitsOnceRetainsKnowledgeAndRestoresCheckpointNotHistory()
        {
            var actor=New("Actor").AddComponent<LoopRewindActor>();actor.transform.position=new Vector3(3,0,0);
            var item=New("Interactable");item.SetActive(false);
            var participant=New("Checkpoint flags").AddComponent<LoopObjectCheckpoint>();participant.Objects=new[]{item};
            var reset=New("Checkpoint").AddComponent<LocalLoopCheckpoint>();reset.Actors=new[]{actor};reset.Participants=new[]{participant};reset.LoopClock=7;
            var c=Controller(reset,actor);var canvas=New("HUD").AddComponent<CanvasGroup>();canvas.alpha=.65f;canvas.interactable=false;c.GameplayUI=new[]{canvas};
            yield return null;
            reset.DiscoveredClueIds.Add("shove_seen");reset.UnlockedDialogueIds.Add("ask_about_recording");
            actor.ClearHistory();actor.transform.position=new Vector3(10,0,0);actor.Record(Time.unscaledTimeAsDouble);item.SetActive(true);reset.LoopClock=91;
            var cameraPose=c.GameplayCamera.transform.position;int restored=0,finished=0;c.CheckpointRestored.AddListener(()=>restored++);c.TransitionCompleted.AddListener(()=>finished++);
            Time.timeScale=0;c.TriggerLoop();c.TriggerLoop();Assert.That(TimeLoopTransitionController.InputBlocked,Is.True);
            yield return Finished(c);
            Assert.That(reset.LoopNumber,Is.EqualTo(2));Assert.That(restored,Is.EqualTo(1));Assert.That(finished,Is.EqualTo(1));
            Assert.That(actor.transform.position.x,Is.EqualTo(3));Assert.That(item.activeSelf,Is.False);Assert.That(reset.LoopClock,Is.EqualTo(7));
            Assert.That(reset.DiscoveredClueIds,Does.Contain("shove_seen"));Assert.That(reset.UnlockedDialogueIds,Does.Contain("ask_about_recording"));
            Assert.That(canvas.alpha,Is.EqualTo(.65f));Assert.That(canvas.interactable,Is.False);Assert.That(c.GameplayCamera.transform.position,Is.EqualTo(cameraPose));
            c.ReducedMotion=true;c.TriggerLoop();c.TriggerLoop();yield return Finished(c);
            Assert.That(reset.LoopNumber,Is.EqualTo(3));Assert.That(restored,Is.EqualTo(2));Assert.That(c.SuccessfulResets,Is.EqualTo(2));
            Assert.That(TimeLoopTransitionController.InputBlocked,Is.False);yield return null;
            Assert.That(GameObject.Find("Time loop transition UI"),Is.Null);
        }
        [UnityTest]
        public IEnumerator SlowResetStaysCoveredAndDoesNotRepeatItsCommand()
        {
            var reset=New("Delayed checkpoint").AddComponent<DelayedLoopTestAdapter>();var c=Controller(reset);c.GameplayCamera.rect=new Rect(.15f,0,.7f,1);
            c.TriggerLoop();float deadline=Time.realtimeSinceStartup+3;
            while(reset.Calls==0){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            for(int frame=0;frame<6;frame++){c.TriggerLoop();yield return null;Assert.That(c.StageIndex,Is.EqualTo(5));Assert.That(c.Progress,Is.EqualTo(.85f));}
            Assert.That(reset.Calls,Is.EqualTo(1));Assert.That(reset.Number,Is.EqualTo(1));
            Assert.That(GameObject.Find("Left viewport cover").GetComponent<Image>().color.a,Is.EqualTo(1));
            Assert.That(GameObject.Find("Right viewport cover").GetComponent<Image>().color.a,Is.EqualTo(1));
            reset.Release=true;yield return Finished(c);
            Assert.That(reset.Number,Is.EqualTo(2));Assert.That(reset.Ends,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator FailureStaysCoveredUntilExplicitCancelAndReleasesItsLock()
        {
            var reset=New("Failed checkpoint").AddComponent<DelayedLoopTestAdapter>();reset.Fail=reset.Release=true;var c=Controller(reset);
            LogAssert.Expect(LogType.Error,"test checkpoint failure");c.TriggerLoop();float end=Time.realtimeSinceStartup+3;
            while(c.LastError==null){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}
            Assert.That(c.IsTransitioning,Is.True);Assert.That(c.StageIndex,Is.EqualTo(5));Assert.That(c.SuccessfulResets,Is.Zero);
            c.CancelTransition();yield return null;Assert.That(c.IsTransitioning,Is.False);Assert.That(reset.Ends,Is.EqualTo(1));Assert.That(reset.Number,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator CancelDuringHistoryAndAfterCommitKeepsConsistentWorld()
        {
            var actor=New("Actor").AddComponent<LoopRewindActor>();actor.transform.position=new Vector3(2,0,0);
            var reset=New("Checkpoint").AddComponent<LocalLoopCheckpoint>();reset.Actors=new[]{actor};var c=Controller(reset,actor);
            c.Stages[3].Duration=.3f;c.Stages[7].Duration=.3f;
            yield return null;actor.transform.position=new Vector3(9,0,0);
            c.TriggerLoop();float end=Time.realtimeSinceStartup+3;
            while(c.StageIndex!=3){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}
            c.CancelTransition();Assert.That(reset.LoopNumber,Is.EqualTo(1));Assert.That(actor.transform.position.x,Is.EqualTo(9));
            c.TriggerLoop();end=Time.realtimeSinceStartup+3;
            while(c.StageIndex!=7){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}
            c.CancelTransition();Assert.That(reset.LoopNumber,Is.EqualTo(2));Assert.That(actor.transform.position.x,Is.EqualTo(2));
            Assert.That(c.SuccessfulResets,Is.EqualTo(1));Assert.That(actor.IsPaused,Is.False);Assert.That(TimeLoopTransitionController.InputBlocked,Is.False);
        }
        [UnityTest]
        public IEnumerator CancelAndDisablePreserveOtherPauseOwnersAndOriginalPose()
        {
            var actor=New("Actor").AddComponent<LoopRewindActor>();var writer=actor.gameObject.AddComponent<TimeLoopTestWriter>();actor.MovementWriters=new[]{writer};
            var preDisabled=New("Pre-disabled writer").AddComponent<TimeLoopTestWriter>();preDisabled.enabled=false;
            var reset=New("Checkpoint").AddComponent<DelayedLoopTestAdapter>();var c=Controller(reset,actor);c.WorldActivity=new Behaviour[]{writer,preDisabled};
            yield return null;
            using(var anotherOwner=new LoopActivityLease())
            {
                anotherOwner.Pause(writer);actor.transform.position=new Vector3(8,0,0);
                c.TriggerLoop();yield return null;actor.transform.position=Vector3.zero;c.enabled=false;yield return null;
                Assert.That(actor.transform.position.x,Is.EqualTo(8));Assert.That(actor.IsPaused,Is.False);Assert.That(writer.enabled,Is.False);Assert.That(preDisabled.enabled,Is.False);
                Assert.That(TimeLoopTransitionController.InputBlocked,Is.False);Assert.That(reset.Calls,Is.Zero);
            }
            Assert.That(writer.enabled,Is.True);Assert.That(preDisabled.enabled,Is.False);
        }
    }
}
