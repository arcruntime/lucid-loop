using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace LucidLoop.Gyms
{
    public sealed class NpcActionState
    {
        public string Type { get; }
        public string TargetId { get; }

        public NpcActionState(string type, string targetId = null)
        {
            if (!IsSupported(type))
                throw new ArgumentException("Unknown NPC action.", nameof(type));
            if (RequiresTarget(type) && !IsValidTarget(type, targetId))
                throw new ArgumentException("Action requires a known target ID.", nameof(targetId));
            Type = type;
            TargetId = RequiresTarget(type) ? targetId : null;
        }

        public static bool IsSupported(string type) => type == "idle" || type == "follow" || type == "wait" ||
            type == "approach" || type == "intervene" || type == "keep_distance" || type == "separate" || type == "fall";
        public static bool RequiresTarget(string type) => type == "follow" || type == "approach" ||
            type == "intervene" || type == "keep_distance" || type == "separate";
        public static bool IsValidTarget(string type, string target) =>
            target == "player" || target == "maya" || target == "ren" || target == "luca" ||
            target == "theo" || target == "affair_partner" || (type == "separate" && target == "vip");
    }

    // A detached, user-safe projection. This class never adjudicates gameplay.
    public sealed class EncounterClientState
    {
        JObject snapshot;
        IReadOnlyDictionary<string, NpcActionState> actors =
            new ReadOnlyDictionary<string, NpcActionState>(new Dictionary<string, NpcActionState>());

        public bool HasSnapshot => snapshot != null;
        public string LoopId { get; private set; }
        public int LoopIndex { get; private set; } = -1;
        public long Revision { get; private set; } = -1;
        public string Mood { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double DurationSeconds { get; private set; }
        public string Phase { get; private set; }
        public bool Recording { get; private set; }
        public IReadOnlyDictionary<string, NpcActionState> Actors => actors;
        public JObject Snapshot => snapshot == null ? null : (JObject)snapshot.DeepClone();
        public JArray PlayerDiscoveries => snapshot == null ? new JArray() : (JArray)snapshot["playerDiscoveries"].DeepClone();

        public bool TryApply(JObject candidate, out string reason)
        {
            reason = "invalid_snapshot";
            if (candidate == null || candidate["loopId"]?.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace((string)candidate["loopId"]) ||
                !TryInteger(candidate["loopIndex"], out var loopIndex) || loopIndex > int.MaxValue ||
                !TryInteger(candidate["revision"], out var revision) ||
                candidate["mood"]?.Type != JTokenType.String ||
                !(candidate["actors"] is JObject actorObject) ||
                !(candidate["playerDiscoveries"] is JArray)) return false;

            string loopId = (string)candidate["loopId"], mood = (string)candidate["mood"];
            if (mood != "Intimate" && mood != "Aggressive") return false;
            if (!TrySeconds(candidate["elapsedSeconds"], out var elapsed) || !TrySeconds(candidate["durationSeconds"], out var duration)) return false;
            if (candidate["phase"] != null && candidate["phase"].Type != JTokenType.String) return false;
            if (candidate["recording"] != null && candidate["recording"].Type != JTokenType.Boolean) return false;
            if (HasSnapshot)
            {
                if (loopIndex < LoopIndex) { reason = "stale_loop"; return false; }
                if ((loopIndex == LoopIndex) != (loopId == LoopId))
                { reason = "loop_identity_mismatch"; return false; }
                if (loopIndex == LoopIndex && revision <= Revision)
                { reason = revision == Revision ? "duplicate_revision" : "stale_revision"; return false; }
            }

            var nextActors = new Dictionary<string, NpcActionState>(StringComparer.Ordinal);
            var projectedActors = new JObject();
            foreach (var property in actorObject.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name) || !(property.Value is JObject actor) ||
                    !(actor["action"] is JObject action) || action["type"]?.Type != JTokenType.String)
                    return false;
                string type = (string)action["type"];
                if (!NpcActionState.IsSupported(type)) return false;
                string target = null;
                if (NpcActionState.RequiresTarget(type))
                {
                    if (action["targetId"]?.Type != JTokenType.String ||
                        !NpcActionState.IsValidTarget(type, target = (string)action["targetId"])) return false;
                }
                nextActors.Add(property.Name, new NpcActionState(type, target));
                var projectedAction = new JObject { ["type"] = type };
                if (target != null) projectedAction["targetId"] = target;
                projectedActors[property.Name] = new JObject { ["action"] = projectedAction };
            }

            // Commit only after complete validation; omit unrelated server fields.
            var nextSnapshot = new JObject
            {
                ["loopId"] = loopId, ["loopIndex"] = loopIndex, ["revision"] = revision,
                ["mood"] = mood, ["actors"] = projectedActors,
                ["playerDiscoveries"] = candidate["playerDiscoveries"].DeepClone()
            };
            snapshot = nextSnapshot;
            nextSnapshot["elapsedSeconds"] = elapsed; nextSnapshot["durationSeconds"] = duration;
            if (candidate["phase"] != null) nextSnapshot["phase"] = candidate["phase"].DeepClone();
            nextSnapshot["recording"] = candidate["recording"] == null ? false : (bool)candidate["recording"];
            actors = new ReadOnlyDictionary<string, NpcActionState>(nextActors);
            LoopId = loopId; LoopIndex = (int)loopIndex; Revision = revision; Mood = mood;
            ElapsedSeconds = elapsed; DurationSeconds = duration; Phase = (string)candidate["phase"];
            Recording = (bool)nextSnapshot["recording"];
            reason = "applied";
            return true;
        }

        static bool TryInteger(JToken token, out long value)
        {
            value = -1;
            if (token?.Type != JTokenType.Integer) return false;
            try { value = token.Value<long>(); return value >= 0; }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException)
            { return false; }
        }

        static bool TrySeconds(JToken token, out double value)
        {
            value = 0;
            if (token == null) return true;
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) return false;
            value = token.Value<double>();
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 86400;
        }
    }
}
