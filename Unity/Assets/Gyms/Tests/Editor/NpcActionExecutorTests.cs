using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace LucidLoop.Gyms.Tests
{
    public sealed class NpcActionExecutorTests
    {
        GameObject actor, target;
        NpcActionExecutor executor;

        [SetUp] public void SetUp()
        {
            actor = new GameObject("NPC action test");
            target = new GameObject("Follow target test");
            executor = actor.AddComponent<NpcActionExecutor>();
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(target);
        }

        [Test] public void WaitClearsFollowTargetAndDoesNotResumeWhenTargetMoves()
        {
            executor.ApplyCommitted(new NpcActionState("follow", "player"), target.transform);
            executor.ApplyCommitted(new NpcActionState("wait"));
            target.transform.position = Vector3.right * 5;
            InvokeCallback("Update");
            Assert.That(executor.Mode, Is.EqualTo("wait"));
            Assert.That(executor.Target, Is.Null);
            Assert.That(executor.LastFailure, Is.Null);
        }

        [Test] public void MissingNavigationReportsFailureOnceWithoutPretendingToMove()
        {
            int failures = 0;
            executor.NavigationFailed += reason => { failures++; Assert.That(reason, Is.EqualTo("agent_not_on_navmesh")); };
            var follow = new NpcActionState("follow", "player");
            executor.ApplyCommitted(follow, target.transform);
            executor.ApplyCommitted(follow, target.transform);
            InvokeCallback("Update");
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(executor.LastFailure, Is.EqualTo("agent_not_on_navmesh"));
            Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test] public void MissingTargetIsReportedAndWaitCanSupersedeFailure()
        {
            executor.ApplyCommitted(new NpcActionState("follow", "player"));
            Assert.That(executor.LastFailure, Is.EqualTo("follow_target_missing"));
            executor.ApplyCommitted(new NpcActionState("wait"));
            Assert.That(executor.LastFailure, Is.Null);
            Assert.That(executor.Mode, Is.EqualTo("wait"));
        }

        [Test] public void DisableCallbackClearsTargetModeAndFailure()
        {
            executor.ApplyCommitted(new NpcActionState("follow", "player"), target.transform);
            InvokeCallback("OnDisable");
            Assert.That(executor.Mode, Is.EqualTo("idle"));
            Assert.That(executor.Target, Is.Null);
            Assert.That(executor.LastFailure, Is.Null);
        }

        // EditMode does not run ordinary MonoBehaviour lifecycle. Invoke the callback body
        // directly; actual engine-driven disable/navigation remains a PlayMode acceptance check.
        void InvokeCallback(string name) => typeof(NpcActionExecutor)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executor, null);
    }
}
