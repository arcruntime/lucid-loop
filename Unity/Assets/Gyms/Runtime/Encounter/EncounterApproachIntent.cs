using System;
using Newtonsoft.Json.Linq;

namespace LucidLoop.Gyms
{
    // One explicit tap, one bounded approach. World eligibility never creates intent.
    public sealed class EncounterApproachIntent
    {
        public string NpcId { get; private set; }
        public string LoopId { get; private set; }
        public long Sequence { get; private set; }
        public bool Pending => NpcId != null;
        public bool Accepted { get; private set; }
        public string Outcome { get; private set; }
        double expires;
        long acceptedFrame;
        public void Begin(string loop, string npc, long sequence, double now)
        { LoopId = loop; NpcId = npc; Sequence = sequence; expires = now + 20; Accepted = false; acceptedFrame = -1; Outcome = "approaching"; }
        public void Cancel(string reason = "approach_cancelled") { NpcId = null; Accepted = false; Outcome = reason; }
        public bool AcceptResult(JObject result)
        {
            if (!Pending || result?["loopId"]?.Type != JTokenType.String || (string)result["loopId"] != LoopId ||
                result["npcId"]?.Type != JTokenType.String || (string)result["npcId"] != NpcId ||
                result["sequence"]?.Type != JTokenType.Integer || result["accepted"]?.Type != JTokenType.Boolean) return false;
            long seq;
            try { seq = result["sequence"].Value<long>(); } catch (Exception) { return false; }
            if (seq != Sequence) return false;
            if (!(bool)result["accepted"]) { Cancel("approach_unavailable"); return true; }
            if (result["frame"]?.Type != JTokenType.Integer) return false;
            try { acceptedFrame = result["frame"].Value<long>(); } catch (Exception) { return false; }
            if (acceptedFrame < 0) return false;
            Accepted = true; return true;
        }
        public string Observe(string loop, long sequence, long frame, bool eligible, string reason, string playerMotion, double now)
        {
            if (!Pending) return null;
            if (loop != LoopId) { Cancel(); return null; }
            if (now >= expires) { Cancel("approach_timed_out"); return null; }
            if (!Accepted || sequence < Sequence) return null;
            if (sequence > Sequence) { Cancel(); return null; }
            if (reason == "encounter_ended") { Cancel("encounter_ended"); return null; }
            if (frame < acceptedFrame) return null;
            if (eligible) { string npc = NpcId; Cancel("conversation_starting"); return npc; }
            if (frame > acceptedFrame && (playerMotion == "idle" || playerMotion == "blocked" || playerMotion == "fallen"))
                Cancel("approach_target_moved");
            return null;
        }
        public bool Tick(double now)
        { if (!Pending || now < expires) return false; Cancel("approach_timed_out"); return true; }
    }

    public static class EncounterConversationEligibility
    {
        // Missing or malformed availability is fail-closed. No client distance inference.
        public static bool TryRead(JObject map, string npc, out bool eligible, out string reason)
        {
            eligible = false; reason = "availability_unknown";
            if (!(map?[npc] is JObject entry) || entry["eligible"]?.Type != JTokenType.Boolean ||
                (entry["reason"] != null && entry["reason"].Type != JTokenType.String)) return false;
            eligible = (bool)entry["eligible"];
            string code = (string)entry["reason"];
            reason = eligible ? null : code == "encounter_ended" ? "encounter_ended" : "out_of_range";
            return true;
        }
    }
}
