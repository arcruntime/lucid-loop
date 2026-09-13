using Newtonsoft.Json.Linq;

namespace LucidLoop.Gyms
{
    public static class EncounterIntentFeedback
    {
        static bool IsTrue(JToken value) => value?.Type == JTokenType.Boolean && value.Value<bool>();
        public static string Describe(JObject result)
        {
            if (result == null) return "The request could not be confirmed. Please try again.";
            // A late speech delivery failure does not undo an authoritative action commit.
            if (IsTrue(result["ok"]) && IsTrue(result["committed"]) && IsTrue(result["outcome"]?["accepted"]))
                return "Request accepted. Leave the conversation to let the night continue.";
            if (!IsTrue(result["ok"]))
                return result["reason"]?.Value<string>() == "busy"
                    ? "Still considering your previous request. Please wait."
                    : "The request could not be confirmed. Please try again.";
            if (result["kind"]?.Value<string>() == "clarification")
                return "Please clarify what you would like them to do.";
            if (result["kind"]?.Value<string>() == "no_action")
                return "Conversation only; no action was taken.";
            return "The request was not carried out. Try a different approach.";
        }
    }
}
