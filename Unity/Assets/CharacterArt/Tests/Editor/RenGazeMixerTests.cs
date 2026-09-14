using System.Collections.Generic;
using NUnit.Framework;

namespace LucidLoop.CharacterArt.Tests
{
 public class RenGazeMixerTests
 {
  [Test] public void ClosedEyeSuppressesOnlyItsGazeWhileSpeechSurvives()
  {
   var input=new Dictionary<string,float>{{"Eyes.gazeLeftL",1},{"Eyes.gazeLeftR",1},{"Face.speech_A",1}};
   var result=RenAssemblyFaceMixer.Mix(input,1,0);
   Assert.That(result["Eyes.gazeLeftL"],Is.Zero);
   Assert.That(result["Eyes.gazeLeftR"],Is.EqualTo(1));
   Assert.That(result["Face.speech_A"],Is.EqualTo(1));
  }
  [Test] public void OppositeDirectionsCancelAndDiagonalCannotExceedAuthoredRadius()
  {
   var input=new Dictionary<string,float>{{"gazeLeftL",1},{"gazeRightL",1},{"gazeUpR",1},{"gazeRightR",1}};
   var result=RenAssemblyFaceMixer.Mix(input,0,0);
   Assert.That(result["gazeLeftL"],Is.Zero);Assert.That(result["gazeRightL"],Is.Zero);
   Assert.That(result["gazeUpR"]*result["gazeUpR"]+result["gazeRightR"]*result["gazeRightR"],Is.EqualTo(1).Within(.00001));
  }
  [Test] public void HalfBlinkUsesQuadraticGazeSuppression()
  {
   var result=RenAssemblyFaceMixer.Mix(new Dictionary<string,float>{{"gazeDownR",1}},0,.5f);
   Assert.That(result["gazeDownR"],Is.EqualTo(.25f));
  }
 }
}
