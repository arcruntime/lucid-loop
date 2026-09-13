using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.CharacterArt.PlayModeTests
{
    public sealed class CharacterFaceDriverPlayModeTests
    {
        readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void CleanBodyMotionFixture()
        {
            foreach (var item in owned) if (item) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator BodyMotionLoopsUntilStoppedAndPreservesExpressionGesture()
        {
            var driver = BodyFixture(out var leg, out var arm);
            var body = Own(RotationClip("Armature/Hips/LeftLeg", "localEulerAnglesRaw.z", 30f, .1f));
            body.SetCurve("Armature/Hips/Spine/LeftArm", typeof(Transform), "localEulerAnglesRaw.z",
                AnimationCurve.Constant(0f, .1f, 15f));
            driver.SetExpression("wave");
            Assert.That(driver.PlayBodyMotion(body, loop: true), Is.True);
            yield return new WaitForSeconds(.35f);

            Assert.That(driver.IsBodyMotionPlaying, Is.True);
            Assert.That(driver.BodyMotionLooping, Is.True);
            Assert.That(driver.ActiveBodyMotionClip, Is.SameAs(body));
            Assert.That(driver.BodyMotionTime, Is.GreaterThanOrEqualTo(0d));
            Assert.That(driver.BodyMotionTime, Is.LessThan(body.length));
            Assert.That(Quaternion.Angle(leg.localRotation, Quaternion.Euler(0f, 0f, 30f)), Is.LessThan(1f));
            Assert.That(Quaternion.Angle(arm.localRotation, Quaternion.Euler(0f, 0f, 45f)), Is.LessThan(3f));

            driver.StopBodyMotion();
            yield return null;
            yield return null;
            Assert.That(driver.IsBodyMotionPlaying, Is.False);
            Assert.That(driver.ActiveBodyMotionClip, Is.Null);
            Assert.That(driver.BodyMotionTime, Is.Zero);
            Assert.That(driver.ActiveExpression.Id, Is.EqualTo("wave"));
            Assert.That(Quaternion.Angle(leg.localRotation, Quaternion.identity), Is.LessThan(1f));
            Assert.That(Quaternion.Angle(arm.localRotation, Quaternion.Euler(0f, 0f, 45f)), Is.LessThan(3f));
        }

        [UnityTest]
        public IEnumerator OneShotReplacesLoopAndReturnsToIdleEvenWhenIdleClockIsPaused()
        {
            var driver = BodyFixture(out var leg, out _);
            var loop = Own(RotationClip("Armature/Hips/LeftLeg", "localEulerAnglesRaw.z", 30f));
            var once = Own(RotationClip("Armature/Hips/LeftLeg", "localEulerAnglesRaw.z", -20f, .2f));
            Assert.That(driver.PlayBodyMotion(loop, loop: true), Is.True);
            yield return null;
            driver.IdleEnabled = false;
            Assert.That(driver.PlayBodyMotion(once), Is.True);
            Assert.That(driver.ActiveBodyMotionClip, Is.SameAs(once));
            Assert.That(driver.BodyMotionLooping, Is.False);
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(leg.localRotation, Quaternion.Euler(0f, 0f, -20f)), Is.LessThan(1f));
            yield return new WaitForSeconds(.3f);
            yield return null;
            Assert.That(driver.IsBodyMotionPlaying, Is.False);
            Assert.That(Quaternion.Angle(leg.localRotation, Quaternion.identity), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator BodyMotionRejectsInvalidInputAndReleasesOnDisable()
        {
            var driver = BodyFixture(out _, out _);
            var clip = Own(RotationClip("Armature/Hips/LeftLeg", "localEulerAnglesRaw.z", 20f));
            var legacy = Own(new AnimationClip { legacy = true });
            Assert.That(driver.PlayBodyMotion(clip, true), Is.True);
            Assert.That(driver.PlayBodyMotion(null), Is.False);
            Assert.That(driver.PlayBodyMotion(legacy), Is.False);
            Assert.That(driver.ActiveBodyMotionClip, Is.SameAs(clip));
            driver.enabled = false;
            Assert.That(driver.IsBodyMotionPlaying, Is.False);
            Assert.That(driver.PlayBodyMotion(clip), Is.False);
            driver.enabled = true;
            Assert.That(driver.PlayBodyMotion(clip), Is.True);
            yield return null;
            driver.Profile.IdleClip = null;
            driver.Bind();
            Assert.That(driver.IsBodyMotionPlaying, Is.False);
            Assert.That(driver.PlayBodyMotion(clip), Is.False, "A motion requires an idle pose to return to.");
        }

        [UnityTest]
        public IEnumerator NavigationOwnsRootAndPriorRootMotionSettingIsRestored()
        {
            var driver = BodyFixture(out _, out _);
            driver.enabled = false;
            var animator = driver.GetComponent<Animator>();
            animator.applyRootMotion = true;
            driver.enabled = true;
            Assert.That(animator.applyRootMotion, Is.False);
            var body = Own(RotationClip("Armature/Hips/LeftLeg", "localEulerAnglesRaw.z", 30f));
            body.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 1f, 100f));
            driver.transform.position = new Vector3(4f, 0f, 2f);
            Assert.That(driver.PlayBodyMotion(body, true), Is.True);
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(driver.transform.position, new Vector3(4f, 0f, 2f)), Is.LessThan(.001f));
            driver.enabled = false;
            Assert.That(animator.applyRootMotion, Is.True);
        }

        CharacterFaceDriver BodyFixture(out Transform leg, out Transform arm)
        {
            var root = Own(new GameObject("Body motion character"));
            var hips = Child(Child(root.transform, "Armature"), "Hips");
            leg = Child(hips, "LeftLeg");
            arm = Child(Child(hips, "Spine"), "LeftArm");
            root.AddComponent<Animator>();
            var profile = Own(ScriptableObject.CreateInstance<CharacterProfile>());
            profile.Id = "body-test";
            profile.IdleClip = Own(RotationClip("Armature/Hips/LeftLeg", "localEulerAnglesRaw.z", 0f));
            profile.Expressions = new[] { new ExpressionPreset {
                Id = "wave", GestureClip = Own(RotationClip("Armature/Hips/Spine/LeftArm", "localEulerAnglesRaw.z", 45f))
            } };
            var driver = root.AddComponent<CharacterFaceDriver>();
            driver.Profile = profile;
            driver.AutomaticBlink = false;
            return driver;
        }

        T Own<T>(T item) where T : Object { owned.Add(item); return item; }

        [UnityTest]
        public IEnumerator HeadExpressionComposesAfterAnimationAndNeutralRestoresAnimatedPose()
        {
            var root = new GameObject("Animated character");
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            root.AddComponent<Animator>();
            var clip = RotationClip("Head", "localEulerAnglesRaw.y", 20f);
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.Id = "head-test";
            profile.IdleClip = clip;
            profile.RestPose.HeadBonePath = "Head";
            profile.Expressions = new[]
            {
                new ExpressionPreset { Id = "neutral", Label = "Neutral" },
                new ExpressionPreset { Id = "look", Label = "Look", HeadEuler = new Vector3(10f, 0f, 0f) }
            };
            var driver = root.AddComponent<CharacterFaceDriver>();
            driver.Profile = profile;
            driver.SetExpression("look");

            yield return null;
            yield return null;

            var expectedComposed = Quaternion.Euler(0f, 20f, 0f) * Quaternion.Euler(10f, 0f, 0f);
            Assert.That(Quaternion.Angle(head.localRotation, expectedComposed), Is.LessThan(1f));

            driver.SetExpression("neutral");
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(head.localRotation, Quaternion.Euler(0f, 20f, 0f)), Is.LessThan(1f));

            Object.Destroy(root);
            Object.Destroy(profile);
            Object.Destroy(clip);
        }

        [UnityTest]
        public IEnumerator ExpressionGestureClipPlaysThroughUpperBodyMask()
        {
            var root = new GameObject("Gesture character");
            var armature = Child(root.transform, "Armature");
            var hips = Child(armature, "Hips");
            var spine = Child(hips, "Spine");
            var arm = Child(spine, "LeftArm");
            root.AddComponent<Animator>();
            var idle = RotationClip("Armature/Hips/Spine/LeftArm", "localEulerAnglesRaw.z", 5f);
            var gesture = RotationClip("Armature/Hips/Spine/LeftArm", "localEulerAnglesRaw.z", 45f);
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.Id = "gesture-test";
            profile.IdleClip = idle;
            profile.Expressions = new[]
            {
                new ExpressionPreset { Id = "neutral", Label = "Neutral" },
                new ExpressionPreset { Id = "wave", Label = "Wave", GestureClip = gesture }
            };
            var driver = root.AddComponent<CharacterFaceDriver>();
            driver.Profile = profile;
            driver.SetExpression("wave");

            yield return new WaitForSeconds(.3f);

            Assert.That(Quaternion.Angle(arm.localRotation, Quaternion.Euler(0f, 0f, 45f)), Is.LessThan(3f));

            Object.Destroy(root);
            Object.Destroy(profile);
            Object.Destroy(idle);
            Object.Destroy(gesture);
        }

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static AnimationClip RotationClip(string path, string property, float value, float duration = 1f)
        {
            var clip = new AnimationClip { name = path + " clip", wrapMode = WrapMode.Loop };
            clip.SetCurve(path, typeof(Transform), property,
                AnimationCurve.Constant(0f, duration, value));
            return clip;
        }
    }
}
