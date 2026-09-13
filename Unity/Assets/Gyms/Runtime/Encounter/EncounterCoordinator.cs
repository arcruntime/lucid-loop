using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // The startup payload includes credentials: consume it directly, never display or log it.
    public sealed class EncounterConversationRequest
    {
        readonly JObject startup;
        public string Address { get; }
        public string CharacterId { get; }
        public string LoopId { get; }
        public JObject Startup => (JObject)startup.DeepClone();
        internal EncounterConversationRequest(string address, string character, string loopId, JObject payload)
        { Address = address; CharacterId = character; LoopId = loopId; startup = (JObject)payload.DeepClone(); }
    }

    public sealed class EncounterCoordinator : MonoBehaviour
    {
        public CharacterActor[] Characters = Array.Empty<CharacterActor>();
        public bool ServerOwnsMovement = true;
        public EncounterClientState State { get; private set; } = new EncounterClientState();
        public bool IsReady { get; private set; }
        public bool IsConnecting { get; private set; }
        public string Status { get; private set; } = "disconnected";
        public bool CanResume => !string.IsNullOrEmpty(gameId) && !string.IsNullOrEmpty(resumeToken);
        public event Action<EncounterClientState> StateChanged;
        public event Action<string> MoodChanged;
        public event Action<string> StatusChanged;
        public event Action<JObject> HistoryReceived;
        public event Action<EncounterConversationRequest> ConversationRequested;
        public event Action ConversationInvalidated;
        public event Action<string, string> NavigationFailed;
        public event Action<JObject> WorldChanged;
        public event Action<bool> PauseChanged;
        public event Action<string> ApproachStatusChanged;
        readonly EncounterApproachIntent approach = new EncounterApproachIntent();
        JObject conversations;
        public string PendingConversationNpc => approach.NpcId;
        public bool ConversationEligibility(string npc, out bool eligible, out string reason)
            => EncounterConversationEligibility.TryRead(conversations, npc, out eligible, out reason);
        public void CancelPendingConversation(bool stopMovement = true)
        { if (!approach.Pending) return; if (stopMovement) StopApproachMovement(); approach.Cancel(); ApproachStatusChanged?.Invoke(approach.Outcome); }
        void StopApproachMovement()
        { if (IsReady && worldLoop == State.LoopId) Send(new JObject { ["type"] = "game.stop", ["loopId"] = State.LoopId, ["sequence"] = ++moveSequence }); }

        readonly Dictionary<string, CharacterActor> actors = new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
        readonly Dictionary<string, NpcActionExecutor> executors = new Dictionary<string, NpcActionExecutor>(StringComparer.Ordinal);
        readonly Dictionary<NpcActionExecutor, Action<string>> handlers = new Dictionary<NpcActionExecutor, Action<string>>();
        EncounterGameConnection connection;
        // Intentionally private non-serialized process memory. No PlayerPrefs or inspector token fields.
        string address, accessToken, gameId, resumeToken;
        float deadline;
        long worldFrame = -1, moveSequence = -1;
        string worldLoop;
        float worldArrival;
        readonly Dictionary<string, Vector3> worldFrom = new Dictionary<string, Vector3>();
        readonly Dictionary<string, Vector3> worldTo = new Dictionary<string, Vector3>();

        public bool BindActors()
        {
            UnbindActors();
            foreach (var actor in Characters)
            {
                if (!actor || string.IsNullOrWhiteSpace(actor.Id) || actors.ContainsKey(actor.Id))
                { UnbindActors(); SetStatus("invalid_actor_registry"); return false; }
                actors.Add(actor.Id, actor);
                if (ServerOwnsMovement)
                {
                    var navigation = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navigation) navigation.enabled = false;
                }
                var executor = actor.GetComponent<NpcActionExecutor>();
                if (!executor) continue;
                string actorId = actor.Id;
                Action<string> handler = reason => NavigationFailed?.Invoke(actorId, reason);
                executor.NavigationFailed += handler;
                handlers.Add(executor, handler); executors.Add(actorId, executor);
            }
            return true;
        }

        public bool ConnectNew(string gameAddress, string serverAccessToken = "") => Connect(gameAddress, serverAccessToken, false);
        public bool Resume(string gameAddress, string serverAccessToken = "") => Connect(gameAddress, serverAccessToken, true);

        bool Connect(string gameAddress, string serverAccessToken, bool resume)
        {
            if (resume && !CanResume) { SetStatus("resume_unavailable"); return false; }
            Uri uri;
            try { uri = EncounterGameConnection.ValidateAddress(gameAddress); }
            catch (Exception) { SetStatus("invalid_game_address"); return false; }
            if (uri.AbsolutePath != "/game") { SetStatus("invalid_game_address"); return false; }
            // Do not disclose a saved resume credential to a different relay host.
            if (resume && address != null && !string.Equals(uri.AbsoluteUri, address, StringComparison.Ordinal))
            { SetStatus("resume_host_mismatch"); return false; }
            Disconnect();
            if (!BindActors()) return false;
            address = uri.AbsoluteUri; accessToken = serverAccessToken ?? "";
            if (!resume) { gameId = resumeToken = null; State = new EncounterClientState(); }
            var startup = new JObject { ["type"] = resume ? "game.resume" : "game.create", ["token"] = accessToken };
            if (resume) { startup["gameId"] = gameId; startup["resumeToken"] = resumeToken; }
            connection = new EncounterGameConnection();
            IsConnecting = true; deadline = Time.unscaledTime + 25f; SetStatus("connecting");
            _ = connection.Connect(address, startup);
            return true;
        }

        void Update()
        {
            var active = connection;
            int budget = 64;
            while (active != null && connection == active && budget-- > 0 && active.TryRead(out var message)) Handle(message);
            if (IsConnecting && Time.unscaledTime > deadline) Fail("startup_timeout");
            if (IsReady && ServerOwnsMovement) RenderWorld();
            if (approach.Tick(Time.unscaledTime)) { StopApproachMovement(); ApproachStatusChanged?.Invoke(approach.Outcome); }
        }

        void Handle(JObject message)
        {
            string type = (string)message["type"];
            if (type == "game.ready")
            {
                if (!IsConnecting || !(message["credentials"] is JObject credentials) ||
                    credentials["gameId"]?.Type != JTokenType.String || credentials["resumeToken"]?.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace((string)credentials["gameId"]) || string.IsNullOrWhiteSpace((string)credentials["resumeToken"]))
                { Fail("invalid_game_ready"); return; }
                if (gameId != null && ((string)credentials["gameId"] != gameId || (string)credentials["resumeToken"] != resumeToken))
                { Fail("resume_identity_mismatch"); return; }
                if (!ApplySnapshot(message["snapshot"] as JObject, true)) return;
                gameId = (string)credentials["gameId"]; resumeToken = (string)credentials["resumeToken"];
                IsConnecting = false; IsReady = true; SetStatus("ready");
            }
            else if (type == "game.state") { if (IsReady) ApplySnapshot(message["snapshot"] as JObject, false); }
            else if (type == "game.history") { if (IsReady) HistoryReceived?.Invoke((JObject)message.DeepClone()); }
            else if (type == "game.world") { if (IsReady) ApplyWorld(message["world"] as JObject); }
            else if (type == "game.approach_result")
            { if (IsReady && approach.AcceptResult(message) && !approach.Pending) ApproachStatusChanged?.Invoke(approach.Outcome); }
            else if (type == "game.pause" && message["paused"]?.Type == JTokenType.Boolean)
            { if ((bool)message["paused"]) CancelPendingConversation(); PauseChanged?.Invoke((bool)message["paused"]); }
            else if (type == "game.move_result" && message["accepted"]?.Type == JTokenType.Boolean && !(bool)message["accepted"]) SetStatus("destination_unavailable");
            else if (type == "game.reset_result" && message["accepted"]?.Type == JTokenType.Boolean && !(bool)message["accepted"]) SetStatus("reset_unavailable");
            else if (type == "game.error")
            {
                // Server codes are identifiers; do not forward arbitrary messages or credential-bearing payloads.
                string code = (string)message["code"];
                if (code == "expired" || code == "unauthorized") { gameId = resumeToken = null; Fail("game_unavailable"); }
                else if (IsConnecting) Fail("game_start_rejected");
                else SetStatus("game_request_rejected");
            }
            else if (type == "game.transport.closed") Fail("game_connection_closed");
        }

        bool ApplySnapshot(JObject snapshot, bool readySnapshot)
        {
            string priorLoop = State.LoopId, priorMood = State.Mood;
            bool applied = State.TryApply(snapshot, out var reason);
            if (!applied && !(readySnapshot && reason == "duplicate_revision"))
            {
                if (reason == "duplicate_revision" || reason == "stale_revision" || reason == "stale_loop") return false;
                Fail("invalid_game_snapshot"); return false;
            }
            if (priorLoop != null && priorLoop != State.LoopId)
            { StopExecutors(); ClearWorld(); ConversationInvalidated?.Invoke(); }
            ApplyActorActions();
            if (priorMood != State.Mood) MoodChanged?.Invoke(State.Mood);
            StateChanged?.Invoke(State);
            return true;
        }

        void ApplyActorActions()
        {
            if (ServerOwnsMovement) { StopExecutors(); return; }
            foreach (var pair in executors)
                if (!State.Actors.ContainsKey(pair.Key)) pair.Value.StopExecution();
            foreach (var pair in State.Actors)
            {
                if (!actors.ContainsKey(pair.Key)) { NavigationFailed?.Invoke(pair.Key, "actor_binding_missing"); continue; }
                if (!executors.TryGetValue(pair.Key, out var executor))
                { if (pair.Value.Type != "idle") NavigationFailed?.Invoke(pair.Key, "action_executor_missing"); continue; }
                Transform target = pair.Value.TargetId != null && actors.TryGetValue(pair.Value.TargetId, out var actor) ? actor.transform : null;
                executor.ApplyCommitted(pair.Value, target);
            }
        }

        public bool RequestSnapshot() => Send(new JObject { ["type"] = "game.snapshot" });
        public bool SendDestination(Vector3 destination)
        {
            // Stop the old approach first: a rejected replacement walk preserves
            // the server's prior destination, so cancellation alone is insufficient.
            CancelPendingConversation();
            if (!IsReady || worldLoop != State.LoopId || !Finite(destination.x) || !Finite(destination.z) ||
                destination.x < -13 || destination.x > 13 || destination.z < -11 || destination.z > 11) return false;
            return Send(new JObject { ["type"] = "game.walk", ["loopId"] = State.LoopId, ["sequence"] = ++moveSequence,
                ["destination"] = new JObject { ["x"] = destination.x, ["z"] = destination.z } });
        }
        public bool Pause(bool paused) { if (paused) CancelPendingConversation(); return Send(new JObject { ["type"] = "game.pause", ["paused"] = paused }); }
        public bool Reset() { CancelPendingConversation(); return Send(new JObject { ["type"] = "game.reset", ["loopId"] = State.LoopId, ["revision"] = State.Revision }); }

        void ApplyWorld(JObject world)
        {
            if (world == null || world["ok"]?.Type != JTokenType.Boolean || !(bool)world["ok"] ||
                world["version"]?.Type != JTokenType.String || world["loopId"]?.Type != JTokenType.String ||
                (string)world["version"] != "club-plan-1" || (string)world["loopId"] != State.LoopId ||
                world["frame"]?.Type != JTokenType.Integer || world["sequence"]?.Type != JTokenType.Integer ||
                !(world["actors"] is JObject body)) return;
            long frame, sequence;
            try { frame = world["frame"].Value<long>(); sequence = world["sequence"].Value<long>(); }
            catch (Exception) { return; }
            if (frame < 0 || frame > 9007199254740991L || sequence < -1 || sequence > 9007199254740991L || (worldLoop == State.LoopId && frame <= worldFrame)) return;
            var targets = new Dictionary<string, Vector3>();
            foreach (var property in body.Properties())
            {
                if (!actors.TryGetValue(property.Name, out var actor) || !(property.Value is JObject actorBody) ||
                    !(actorBody["position"] is JObject position) ||
                    !TryCoordinate(position["x"], -13, 13, out var x) || !TryCoordinate(position["z"], -11, 11, out var z)) return;
                if (actorBody["motion"]?.Type != JTokenType.String) return;
                string motion = (string)actorBody["motion"];
                if (motion != "moving" && motion != "idle" && motion != "blocked" && motion != "fallen") return;
                targets.Add(property.Name, new Vector3(x, actor.transform.position.y, z));
            }
            if (targets.Count != actors.Count) return;
            bool snap = worldLoop != State.LoopId;
            worldLoop = State.LoopId; worldFrame = frame; moveSequence = Math.Max(moveSequence, sequence);
            worldFrom.Clear(); worldTo.Clear();
            foreach (var pair in targets)
            {
                var actor = actors[pair.Key];
                worldFrom[pair.Key] = snap ? pair.Value : actor.transform.position; worldTo[pair.Key] = pair.Value;
                if (snap) actor.transform.position = pair.Value;
            }
            worldArrival = Time.unscaledTime;
            conversations = world["conversations"] is JObject availability ? (JObject)availability.DeepClone() : null;
            WorldChanged?.Invoke((JObject)world.DeepClone());
            if (approach.Pending)
            {
                ConversationEligibility(approach.NpcId, out var eligible, out var unavailable);
                string npc = approach.Observe(worldLoop, sequence, frame, eligible, unavailable, (string)body["player"]?["motion"], Time.unscaledTime);
                if (!approach.Pending) ApproachStatusChanged?.Invoke(approach.Outcome);
                if (npc != null) OpenConversation(npc);
            }
        }
        void RenderWorld()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - worldArrival) / .1f);
            foreach (var pair in worldTo)
            {
                if (!actors.TryGetValue(pair.Key, out var actor) || !actor) continue;
                var position = Vector3.Lerp(worldFrom[pair.Key], pair.Value, progress);
                var direction = pair.Value - worldFrom[pair.Key]; direction.y = 0;
                actor.transform.position = position;
                if (direction.sqrMagnitude > .0001f) actor.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
        void ClearWorld() { CancelPendingConversation(); conversations = null; worldFrame = moveSequence = -1; worldLoop = null; worldFrom.Clear(); worldTo.Clear(); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool TryCoordinate(JToken token, float min, float max, out float value)
        {
            value = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return false;
            try { value = token.Value<float>(); } catch (Exception) { return false; }
            return Finite(value) && value >= min && value <= max;
        }
        public bool RequestHistory(string npcId = null, int? loopIndex = null)
        {
            if ((npcId != null && !IsTalkable(npcId)) || loopIndex < 0) return false;
            var request = new JObject { ["type"] = "game.history" };
            if (npcId != null) request["npcId"] = npcId;
            if (loopIndex.HasValue) request["loopIndex"] = loopIndex.Value;
            return Send(request);
        }

        public bool RequestConversation(string npcId)
        {
            CancelPendingConversation();
            if (!IsReady || !CanResume || !IsTalkable(npcId) || !actors.ContainsKey(npcId) || worldLoop != State.LoopId) return false;
            ConversationEligibility(npcId, out _, out var reason);
            if (reason == "encounter_ended") { ApproachStatusChanged?.Invoke(reason); return false; }
            ConversationInvalidated?.Invoke();
            long sequence = ++moveSequence;
            approach.Begin(State.LoopId, npcId, sequence, Time.unscaledTime);
            if (!Send(new JObject { ["type"] = "game.approach", ["loopId"] = State.LoopId, ["sequence"] = sequence, ["npcId"] = npcId }))
            { CancelPendingConversation(); return false; }
            ApproachStatusChanged?.Invoke("approaching");
            return true;
        }

        bool OpenConversation(string npcId)
        {
            if (!IsReady || !CanResume || !IsTalkable(npcId) || !actors.ContainsKey(npcId)) return false;
            var live = new UriBuilder(address) { Path = "/live" }.Uri.AbsoluteUri;
            var payload = new JObject { ["type"] = "gym.start", ["character"] = npcId, ["token"] = accessToken,
                ["gameId"] = gameId, ["resumeToken"] = resumeToken };
            ConversationInvalidated?.Invoke();
            ConversationRequested?.Invoke(new EncounterConversationRequest(live, npcId, State.LoopId, payload));
            return true;
        }

        static bool IsTalkable(string id) => id == "maya" || id == "ren" || id == "luca" || id == "theo";
        bool Send(JObject message) { if (!IsReady || connection == null) return false; _ = connection.Send(message); return true; }
        void SetStatus(string value) { Status = value; StatusChanged?.Invoke(value); }
        void StopExecutors() { foreach (var executor in executors.Values) if (executor) executor.StopExecution(); }
        void UnbindActors()
        {
            StopExecutors();
            foreach (var pair in handlers) if (pair.Key) pair.Key.NavigationFailed -= pair.Value;
            handlers.Clear(); executors.Clear(); actors.Clear();
        }
        void Fail(string code) { Disconnect(); SetStatus(code); }
        public void Disconnect()
        {
            var prior = connection; connection = null; prior?.Dispose();
            ClearWorld();
            IsReady = IsConnecting = false; StopExecutors(); ConversationInvalidated?.Invoke(); SetStatus("disconnected");
        }
        void OnApplicationPause(bool paused) { if (paused) Disconnect(); }
        void OnDisable() { Disconnect(); UnbindActors(); }
    }
}
