using NUnit.Framework;
using UnityEngine;
using LucidLoop.Gyms.Mvp;
namespace LucidLoop.Gyms.Tests
{
 public class LoopStateTests
 {
  [Test] public void RewindRetainsMemoryButClearsTransientChoices()
  {
   var s=new LoopState();Assert.IsTrue(s.Recognize());Assert.IsFalse(s.Recognize());s.Rewind();
   s.Wait(true);s.PrepareMaya();s.PrepareLuca();s.SetMusic(true);s.Recognize();s.Rewind();
   Assert.IsTrue(s.RemembersRecording);Assert.AreEqual(3,s.Loop);Assert.IsFalse(s.MayaWaiting);
   Assert.IsFalse(s.Intimate);Assert.IsFalse(s.CanPrevent);Assert.IsFalse(s.Recognized);
  }
  [Test] public void PreparationAloneDoesNotWinAndMusicIsNotASolveButton()
  {
   var s=new LoopState();s.Recognize();s.Rewind();s.SetMusic(true);s.Wait(true);
   Assert.IsFalse(s.Resolve());Assert.IsFalse(s.CanPrevent);s.PrepareMaya();s.PrepareLuca();
   Assert.IsFalse(s.Resolve());s.Recognize();Assert.IsTrue(s.Resolve());
  }
  [Test] public void SingleInterventionIsNotEnoughForThisAuthoredCheckpoint()
  {
   var s=new LoopState();s.Recognize();s.Rewind();s.PrepareMaya();s.Recognize();Assert.IsFalse(s.Resolve());
  }
  [Test] public void EntranceAndBarAreOutsideRecognitionArea()
  {
   Assert.IsFalse(FirstLoop.InRecognitionArea(new Vector3(0,0,-9)));
   Assert.IsFalse(FirstLoop.InRecognitionArea(new Vector3(-5,0,-3)));
   Assert.IsTrue(FirstLoop.InRecognitionArea(new Vector3(4,0,-3)));
  }
 }
}
