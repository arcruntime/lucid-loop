using System;
using UnityEngine;
using UnityEngine.AI;

namespace LucidLoop.Gyms
{
    // Executes a committed action only. Loop/revision validation belongs to the coordinator.
    // Does not own an Animator, story outcome, or permission to resume following.
    public sealed class NpcActionExecutor : MonoBehaviour
    {
        public NavMeshAgent Agent;
        public float RepathInterval = .25f;
        public float TargetSampleRadius = 1f;
        public string Mode { get; private set; } = "idle";
        public Transform Target { get; private set; }
        public string LastFailure { get; private set; }
        public event Action<string> NavigationFailed;
        float nextRepath;
        bool awaitingPath;
        bool hasApplied;

        void Awake() { if (!Agent) Agent = GetComponent<NavMeshAgent>(); }

        public void ApplyCommitted(NpcActionState action, Transform resolvedTarget = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Type != "idle" && action.Type != "follow" && action.Type != "wait")
            { StopExecution(); Fail("action_requires_world_adapter"); return; }
            if (hasApplied && Mode == action.Type && Target == (action.Type == "follow" ? resolvedTarget : null))
            {
                if (Mode != "follow") StopAgent();
                return;
            }
            StopAgent();
            hasApplied = true;
            Mode = action.Type;
            Target = action.Type == "follow" ? resolvedTarget : null;
            LastFailure = null;
            nextRepath = 0f;
            if (Mode == "follow") TickFollow();
        }

        void Update()
        {
            if (Mode == "follow" && LastFailure == null) TickFollow();
        }

        void TickFollow()
        {
            if (!Target) { Fail("follow_target_missing"); return; }
            if (!Agent || !Agent.enabled || !Agent.isOnNavMesh) { Fail("agent_not_on_navmesh"); return; }
            if (awaitingPath)
            {
                if (Agent.pathPending) return;
                awaitingPath = false;
                if (Agent.pathStatus != NavMeshPathStatus.PathComplete) { Fail("follow_path_incomplete"); return; }
            }
            if (Time.unscaledTime < nextRepath) return;
            nextRepath = Time.unscaledTime + Mathf.Max(.05f, RepathInterval);
            var delta = Target.position - Agent.transform.position;
            delta.y = 0f;
            float stopDistance = Mathf.Max(.1f, Agent.stoppingDistance) + .15f;
            if (delta.sqrMagnitude <= stopDistance * stopDistance) { StopAgent(); return; }
            if (!NavMesh.SamplePosition(Target.position, out var hit, Mathf.Max(.05f, TargetSampleRadius), Agent.areaMask))
            { Fail("follow_target_unreachable"); return; }
            Agent.isStopped = false;
            if (!Agent.SetDestination(hit.position)) { Fail("follow_path_rejected"); return; }
            awaitingPath = true;
        }

        void Fail(string reason)
        {
            StopAgent();
            LastFailure = reason;
            NavigationFailed?.Invoke(reason);
        }

        void StopAgent()
        {
            awaitingPath = false;
            if (Agent && Agent.enabled && Agent.isOnNavMesh)
            { Agent.isStopped = true; Agent.ResetPath(); }
        }

        public void StopExecution()
        {
            StopAgent();
            Mode = "idle"; Target = null; LastFailure = null; nextRepath = 0f; hasApplied = false;
        }

        void OnDisable() => StopExecution();
    }
}
