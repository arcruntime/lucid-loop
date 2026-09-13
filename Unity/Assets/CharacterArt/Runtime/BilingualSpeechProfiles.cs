using System;
using System.Collections.Generic;

namespace LucidLoop.CharacterArt
{
    public enum SpeechLanguage
    {
        English,
        Japanese
    }

    public enum SpeechContactBehavior
    {
        Rest,
        LipSeal,
        Labiodental,
        Interdental,
        Alveolar,
        Rhotic,
        Tap,
        Velar,
        Fricative,
        Affricate,
        Nasal,
        GlottalOpen,
        Glide,
        VowelOpen,
        VowelSpread,
        VowelCentral,
        VowelRounded,
        VowelCompressed,
        GeminateHold
    }

    [Serializable]
    public struct SpeechTargetContribution
    {
        public string ShapeName;
        public float Weight;

        public SpeechTargetContribution(string shapeName, float weight)
        {
            ShapeName = shapeName;
            Weight = weight;
        }
    }

    [Serializable]
    public sealed class SpeechPoseProfile
    {
        public SpeechLanguage Language;
        public string Id;
        public string Label;
        public SpeechContactBehavior Contact;
        public string ContactNotes;
        public SpeechTargetContribution[] Targets = Array.Empty<SpeechTargetContribution>();
    }

    [Serializable]
    public struct SpeechReviewStep
    {
        public SpeechLanguage Language;
        public string PoseId;
        public float HoldSeconds;
        public float TransitionSeconds;
        public string Context;

        public SpeechReviewStep(string poseId, float holdSeconds, float transitionSeconds)
        {
            Language = SpeechLanguage.English;
            PoseId = poseId;
            HoldSeconds = holdSeconds;
            TransitionSeconds = transitionSeconds;
            Context = string.Empty;
        }

        public SpeechReviewStep(SpeechLanguage language, string poseId, float holdSeconds,
            float transitionSeconds, string context = "")
        {
            Language = language;
            PoseId = poseId;
            HoldSeconds = holdSeconds;
            TransitionSeconds = transitionSeconds;
            Context = context;
        }
    }

    [Serializable]
    public sealed class SpeechReviewSequence
    {
        public string Id;
        public string Label;
        public SpeechLanguage Language;
        public string ReviewText;
        public SpeechReviewStep[] Steps = Array.Empty<SpeechReviewStep>();
    }

    public readonly struct SpeechTimelineFrame
    {
        public readonly SpeechLanguage FromLanguage;
        public readonly string FromPoseId;
        public readonly SpeechLanguage ToLanguage;
        public readonly string ToPoseId;
        public readonly float Blend;
        public readonly bool IsComplete;

        public SpeechTimelineFrame(SpeechReviewStep from, SpeechReviewStep to, float blend, bool isComplete)
        {
            FromLanguage = from.Language;
            FromPoseId = from.PoseId;
            ToLanguage = to.Language;
            ToPoseId = to.PoseId;
            Blend = blend < 0f ? 0f : blend > 1f ? 1f : blend;
            IsComplete = isComplete;
        }
    }

    public sealed class SpeechReviewTimeline
    {
        const float Epsilon = .000001f;
        readonly SpeechReviewSequence sequence;
        int stepIndex;
        float stepTime;
        bool complete;

        public SpeechReviewTimeline(SpeechReviewSequence sequence)
        {
            this.sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            if (sequence.Steps == null || sequence.Steps.Length == 0)
                throw new ArgumentException("A speech review sequence requires at least one step.", nameof(sequence));
        }

        public SpeechTimelineFrame Advance(float deltaSeconds)
        {
            var remaining = Math.Max(0f, deltaSeconds);
            for (var transitions = 0; transitions < 4096; transitions++)
            {
                var current = sequence.Steps[stepIndex];
                if (stepIndex == sequence.Steps.Length - 1)
                {
                    stepTime += remaining;
                    complete = stepTime + Epsilon >= Math.Max(0f, current.HoldSeconds);
                    return new SpeechTimelineFrame(current, current, 0f, complete);
                }

                var hold = Math.Max(0f, current.HoldSeconds);
                if (stepTime + remaining + Epsilon < hold)
                {
                    stepTime += remaining;
                    return new SpeechTimelineFrame(current, current, 0f, false);
                }

                if (stepTime < hold)
                {
                    remaining -= hold - stepTime;
                    stepTime = hold;
                }

                var next = sequence.Steps[stepIndex + 1];
                var transition = Math.Max(0f, current.TransitionSeconds);
                var transitionTime = stepTime - hold;
                if (transition > Epsilon && transitionTime + remaining + Epsilon < transition)
                {
                    stepTime += remaining;
                    return new SpeechTimelineFrame(current, next, (transitionTime + remaining) / transition, false);
                }

                if (transition > Epsilon) remaining -= Math.Max(0f, transition - transitionTime);
                stepIndex++;
                stepTime = 0f;
                if (remaining <= Epsilon)
                    return new SpeechTimelineFrame(next, next, 0f, false);
            }
            throw new InvalidOperationException("Speech timeline exceeded the transition safety limit.");
        }
    }

    public static class BilingualSpeechLibrary
    {
        public static SpeechPoseProfile[] CreateDefaultPoses()
        {
            return new[]
            {
                Pose(SpeechLanguage.English, "sil", "SILENCE", SpeechContactBehavior.Rest, "Rest; clears the active snapshot."),
                Pose(SpeechLanguage.English, "pbm", "P / B / M", SpeechContactBehavior.LipSeal, "Complete lip seal retained through release.", Target("viseme_PP")),
                Pose(SpeechLanguage.English, "fv", "F / V", SpeechContactBehavior.Labiodental, "Lower lip contacts upper incisors.", Target("viseme_FF")),
                Pose(SpeechLanguage.English, "th", "TH", SpeechContactBehavior.Interdental, "Tongue-tip dental/interdental contact.", Target("viseme_TH")),
                Pose(SpeechLanguage.English, "tdn", "T / D / N", SpeechContactBehavior.Alveolar, "Alveolar tongue contact and release.", Target("viseme_DD")),
                Pose(SpeechLanguage.English, "kgng", "K / G / NG", SpeechContactBehavior.Velar, "Posterior tongue contact.", Target("viseme_kk")),
                Pose(SpeechLanguage.English, "chj", "CH / J", SpeechContactBehavior.Affricate, "Postalveolar closure and timed release.", Target("viseme_CH")),
                Pose(SpeechLanguage.English, "sz", "S / Z", SpeechContactBehavior.Fricative, "Narrow alveolar frication.", Target("viseme_SS")),
                Pose(SpeechLanguage.English, "shzh", "SH / ZH", SpeechContactBehavior.Fricative, "Rounded postalveolar frication.", Target("viseme_CH", .55f), Target("viseme_SS", .45f)),
                Pose(SpeechLanguage.English, "n", "N", SpeechContactBehavior.Nasal, "Non-bilabial nasal baseline.", Target("viseme_nn")),
                Pose(SpeechLanguage.English, "r", "R (EN)", SpeechContactBehavior.Rhotic, "Held English rhotic; never used for Japanese tap.", Target("viseme_RR")),
                Pose(SpeechLanguage.English, "l", "L (EN)", SpeechContactBehavior.Alveolar, "Distinct lateral alveolar contact.", Target("viseme_en_L")),
                Pose(SpeechLanguage.English, "a", "A / AA", SpeechContactBehavior.VowelOpen, "Open vowel family.", Target("viseme_aa")),
                Pose(SpeechLanguage.English, "e", "E", SpeechContactBehavior.VowelSpread, "Mid/front spread vowel family.", Target("viseme_E")),
                Pose(SpeechLanguage.English, "i", "I / EE", SpeechContactBehavior.VowelSpread, "High/front spread vowel family.", Target("viseme_I")),
                Pose(SpeechLanguage.English, "o", "O", SpeechContactBehavior.VowelRounded, "Rounded mid/back vowel family.", Target("viseme_O")),
                Pose(SpeechLanguage.English, "u", "U (EN)", SpeechContactBehavior.VowelRounded, "Protruded English rounded vowel.", Target("viseme_U")),
                Pose(SpeechLanguage.English, "schwa", "CENTRAL ə", SpeechContactBehavior.VowelCentral, "Relaxed central vowel calibrated between the open and mid baselines.", Target("viseme_E", .24f), Target("viseme_aa", .22f)),
                Pose(SpeechLanguage.English, "h", "H", SpeechContactBehavior.GlottalOpen, "Vowel-conditioned open glottal pose; no forced closure.", Target("viseme_aa", .18f)),
                Pose(SpeechLanguage.English, "y", "Y GLIDE", SpeechContactBehavior.Glide, "Front-vowel-conditioned palatal glide.", Target("viseme_I", .65f)),
                Pose(SpeechLanguage.English, "w", "W (EN)", SpeechContactBehavior.Glide, "Protruded rounded glide transitioning into its following vowel.", Target("viseme_U", .72f)),

                Pose(SpeechLanguage.Japanese, "sil", "SILENCE", SpeechContactBehavior.Rest, "Rest; clears the active snapshot."),
                Pose(SpeechLanguage.Japanese, "a", "A / あ", SpeechContactBehavior.VowelOpen, "Japanese A calibrated from baseline open vowel.", Target("viseme_aa")),
                Pose(SpeechLanguage.Japanese, "i", "I / い", SpeechContactBehavior.VowelSpread, "Japanese I calibrated from baseline front vowel.", Target("viseme_I")),
                Pose(SpeechLanguage.Japanese, "u", "U / う", SpeechContactBehavior.VowelCompressed, "Japanese compressed U with less protrusion than English U.", Target("viseme_ja_U")),
                Pose(SpeechLanguage.Japanese, "e", "E / え", SpeechContactBehavior.VowelSpread, "Japanese E calibrated from baseline mid vowel.", Target("viseme_E")),
                Pose(SpeechLanguage.Japanese, "o", "O / お", SpeechContactBehavior.VowelRounded, "Japanese O calibrated from baseline rounded vowel.", Target("viseme_O")),
                Pose(SpeechLanguage.Japanese, "pbm", "P / B / M", SpeechContactBehavior.LipSeal, "Bilabial seal with vowel-conditioned release.", Target("viseme_PP")),
                Pose(SpeechLanguage.Japanese, "fu", "FU / ふ", SpeechContactBehavior.Fricative, "Bilabial frication; never English F/V dental contact.", Target("viseme_ja_FU")),
                Pose(SpeechLanguage.Japanese, "tdn", "T / D / N", SpeechContactBehavior.Alveolar, "Alveolar contact conditioned by following vowel.", Target("viseme_DD")),
                Pose(SpeechLanguage.Japanese, "r_tap", "TAP R / ら", SpeechContactBehavior.Tap, "Brief Japanese tongue tap; never held English rhotic.", Target("viseme_ja_R")),
                Pose(SpeechLanguage.Japanese, "kg", "K / G", SpeechContactBehavior.Velar, "Posterior contact with neighboring vowel shaping.", Target("viseme_kk")),
                Pose(SpeechLanguage.Japanese, "sz", "S / Z", SpeechContactBehavior.Fricative, "Alveolar frication.", Target("viseme_SS")),
                Pose(SpeechLanguage.Japanese, "shj", "SHI / JI", SpeechContactBehavior.Fricative, "Palatalized frication.", Target("viseme_CH", .45f), Target("viseme_SS", .55f)),
                Pose(SpeechLanguage.Japanese, "ch", "CHI", SpeechContactBehavior.Affricate, "Palatalized affricate closure and release.", Target("viseme_CH")),
                Pose(SpeechLanguage.Japanese, "tsu", "TSU / つ", SpeechContactBehavior.Affricate, "Alveolar affricate released into compressed U.", Target("viseme_CH", .55f), Target("viseme_ja_U", .45f)),
                Pose(SpeechLanguage.Japanese, "h", "HA/HE/HO / は", SpeechContactBehavior.GlottalOpen, "Vowel-conditioned glottal opening without a forced oral closure.", Target("viseme_aa", .16f)),
                Pose(SpeechLanguage.Japanese, "hi", "HI / ひ", SpeechContactBehavior.GlottalOpen, "Front-vowel-conditioned Japanese H variant.", Target("viseme_I", .35f)),
                Pose(SpeechLanguage.Japanese, "y", "Y GLIDE / や", SpeechContactBehavior.Glide, "Palatal glide into a Japanese vowel.", Target("viseme_I", .62f)),
                Pose(SpeechLanguage.Japanese, "w", "W GLIDE / わ", SpeechContactBehavior.Glide, "Compressed/rounded glide calibrated for Japanese.", Target("viseme_ja_U", .55f), Target("viseme_O", .2f)),
                Pose(SpeechLanguage.Japanese, "v", "V / ヴ", SpeechContactBehavior.Fricative, "Voiced loanword bilabial frication; shares visible contact with Japanese FU, not English F/V.", Target("viseme_ja_FU")),
                Pose(SpeechLanguage.Japanese, "n_bilabial", "N: LABIAL", SpeechContactBehavior.Nasal, "Moraic nasal before bilabials; uses lip seal.", Target("viseme_PP")),
                Pose(SpeechLanguage.Japanese, "n_alveolar", "N: ALV.", SpeechContactBehavior.Nasal, "Moraic nasal before alveolars.", Target("viseme_nn")),
                Pose(SpeechLanguage.Japanese, "n_velar", "N: VELAR", SpeechContactBehavior.Nasal, "Moraic nasal before velars.", Target("viseme_kk", .72f), Target("viseme_nn", .28f)),
                Pose(SpeechLanguage.Japanese, "geminate", "GEMINATE", SpeechContactBehavior.GeminateHold, "Hold the following consonant contact/frication; release is sequence-timed.", Target("viseme_DD"))
            };
        }

        public static SpeechReviewSequence[] CreateDefaultSequences()
        {
            return new[]
            {
                Sequence("en_diphthong_ai", "EN: A → I", SpeechLanguage.English, "eye / aɪ",
                    Step(SpeechLanguage.English, "a", .10f, .12f), Step(SpeechLanguage.English, "i", .14f, 0f)),
                Sequence("en_l_vs_r", "EN: L vs R", SpeechLanguage.English, "light / right",
                    Step(SpeechLanguage.English, "l", .12f, .05f), Step(SpeechLanguage.English, "sil", .05f, .04f), Step(SpeechLanguage.English, "r", .16f, 0f)),
                Sequence("en_pbm", "EN: P/B/M SEAL", SpeechLanguage.English, "map / bob / pop",
                    Step(SpeechLanguage.English, "a", .08f, .04f), Step(SpeechLanguage.English, "pbm", .13f, .04f, "hold full seal"), Step(SpeechLanguage.English, "a", .10f, 0f)),
                Sequence("en_fricatives", "EN: F/V TH SH", SpeechLanguage.English, "five / this / vision",
                    Step(SpeechLanguage.English, "fv", .13f, .05f), Step(SpeechLanguage.English, "th", .13f, .05f), Step(SpeechLanguage.English, "shzh", .14f, 0f)),
                Sequence("en_vowels", "EN: VOWEL RANGE", SpeechLanguage.English, "open / spread / central / rounded",
                    Step(SpeechLanguage.English, "a", .12f, .06f), Step(SpeechLanguage.English, "i", .12f, .06f), Step(SpeechLanguage.English, "schwa", .12f, .06f), Step(SpeechLanguage.English, "o", .12f, .06f), Step(SpeechLanguage.English, "u", .12f, 0f)),
                Sequence("en_connected", "EN: CONNECTED", SpeechLanguage.English, "we really value the choice",
                    Step(SpeechLanguage.English, "w", .06f, .035f), Step(SpeechLanguage.English, "i", .07f, .03f), Step(SpeechLanguage.English, "r", .07f, .03f), Step(SpeechLanguage.English, "schwa", .055f, .025f), Step(SpeechLanguage.English, "l", .06f, .03f), Step(SpeechLanguage.English, "fv", .065f, .025f), Step(SpeechLanguage.English, "chj", .07f, 0f)),

                Sequence("ja_vowels", "JA: あいうえお", SpeechLanguage.Japanese, "あいうえお",
                    Step(SpeechLanguage.Japanese, "a", .14f, .06f), Step(SpeechLanguage.Japanese, "i", .14f, .06f), Step(SpeechLanguage.Japanese, "u", .14f, .06f), Step(SpeechLanguage.Japanese, "e", .14f, .06f), Step(SpeechLanguage.Japanese, "o", .14f, 0f)),
                Sequence("ja_pbm_rows", "JA: ぱばま ROWS", SpeechLanguage.Japanese, "ぱぴぷぺぽ / ばびぶべぼ / まみむめも",
                    Step(SpeechLanguage.Japanese, "pbm", .12f, .035f, "retain complete seal"), Step(SpeechLanguage.Japanese, "a", .09f, .035f), Step(SpeechLanguage.Japanese, "pbm", .12f, .035f), Step(SpeechLanguage.Japanese, "i", .09f, .035f), Step(SpeechLanguage.Japanese, "pbm", .12f, .035f), Step(SpeechLanguage.Japanese, "u", .09f, .035f), Step(SpeechLanguage.Japanese, "pbm", .12f, .035f), Step(SpeechLanguage.Japanese, "e", .09f, .035f), Step(SpeechLanguage.Japanese, "pbm", .12f, .035f), Step(SpeechLanguage.Japanese, "o", .09f, 0f)),
                Sequence("ja_fu_loanwords", "JA: ふ / ファフィフェフォ", SpeechLanguage.Japanese, "ふ ファ フィ フェ フォ",
                    Step(SpeechLanguage.Japanese, "fu", .11f, .04f), Step(SpeechLanguage.Japanese, "a", .10f, .04f), Step(SpeechLanguage.Japanese, "fu", .11f, .04f), Step(SpeechLanguage.Japanese, "i", .10f, .04f), Step(SpeechLanguage.Japanese, "fu", .11f, .04f), Step(SpeechLanguage.Japanese, "e", .10f, .04f), Step(SpeechLanguage.Japanese, "fu", .11f, .04f), Step(SpeechLanguage.Japanese, "o", .10f, 0f)),
                Sequence("ja_r_tap", "JA: らりるれろ", SpeechLanguage.Japanese, "らりるれろ",
                    Step(SpeechLanguage.Japanese, "r_tap", .055f, .035f), Step(SpeechLanguage.Japanese, "a", .09f, .035f), Step(SpeechLanguage.Japanese, "r_tap", .055f, .035f), Step(SpeechLanguage.Japanese, "i", .09f, .035f), Step(SpeechLanguage.Japanese, "r_tap", .055f, .035f), Step(SpeechLanguage.Japanese, "u", .09f, .035f), Step(SpeechLanguage.Japanese, "r_tap", .055f, .035f), Step(SpeechLanguage.Japanese, "e", .09f, .035f), Step(SpeechLanguage.Japanese, "r_tap", .055f, .035f), Step(SpeechLanguage.Japanese, "o", .09f, 0f)),
                Sequence("ja_n_sampo", "JA N: さんぽ", SpeechLanguage.Japanese, "さんぽ",
                    Step(SpeechLanguage.Japanese, "a", .10f, .04f), Step(SpeechLanguage.Japanese, "n_bilabial", .12f, .03f, "ん before p"), Step(SpeechLanguage.Japanese, "pbm", .10f, .04f), Step(SpeechLanguage.Japanese, "o", .11f, 0f)),
                Sequence("ja_n_sanda", "JA N: さんだ", SpeechLanguage.Japanese, "さんだ",
                    Step(SpeechLanguage.Japanese, "a", .10f, .04f), Step(SpeechLanguage.Japanese, "n_alveolar", .12f, .03f, "ん before d"), Step(SpeechLanguage.Japanese, "tdn", .09f, .04f), Step(SpeechLanguage.Japanese, "a", .11f, 0f)),
                Sequence("ja_n_sanka", "JA N: さんか", SpeechLanguage.Japanese, "さんか",
                    Step(SpeechLanguage.Japanese, "a", .10f, .04f), Step(SpeechLanguage.Japanese, "n_velar", .12f, .03f, "ん before k"), Step(SpeechLanguage.Japanese, "kg", .09f, .04f), Step(SpeechLanguage.Japanese, "a", .11f, 0f)),
                Sequence("ja_geminate", "JA: きって / きっぷ", SpeechLanguage.Japanese, "きって きっぷ がっこう",
                    Step(SpeechLanguage.Japanese, "i", .09f, .03f), Step(SpeechLanguage.Japanese, "geminate", .16f, .025f, "hold following consonant contact"), Step(SpeechLanguage.Japanese, "tdn", .07f, .035f), Step(SpeechLanguage.Japanese, "e", .11f, 0f)),
                Sequence("ja_yoon", "JA: きゃきゅきょ", SpeechLanguage.Japanese, "きゃきゅきょ / にゃにゅにょ",
                    Step(SpeechLanguage.Japanese, "kg", .07f, .025f), Step(SpeechLanguage.Japanese, "y", .07f, .025f), Step(SpeechLanguage.Japanese, "a", .10f, .04f), Step(SpeechLanguage.Japanese, "kg", .07f, .025f), Step(SpeechLanguage.Japanese, "y", .07f, .025f), Step(SpeechLanguage.Japanese, "u", .10f, .04f), Step(SpeechLanguage.Japanese, "kg", .07f, .025f), Step(SpeechLanguage.Japanese, "y", .07f, .025f), Step(SpeechLanguage.Japanese, "o", .10f, 0f)),
                Sequence("ja_shi_chi_tsu", "JA: し ち つ", SpeechLanguage.Japanese, "し / ち / つ",
                    Step(SpeechLanguage.Japanese, "shj", .11f, .035f), Step(SpeechLanguage.Japanese, "i", .08f, .04f), Step(SpeechLanguage.Japanese, "ch", .105f, .03f), Step(SpeechLanguage.Japanese, "i", .08f, .04f), Step(SpeechLanguage.Japanese, "tsu", .12f, .03f), Step(SpeechLanguage.Japanese, "u", .07f, 0f)),
                Sequence("ja_long_vowels", "JA: LONG VOWELS", SpeechLanguage.Japanese, "おばさん / おばあさん / おじさん / おじいさん",
                    Step(SpeechLanguage.Japanese, "o", .11f, .035f), Step(SpeechLanguage.Japanese, "pbm", .08f, .03f), Step(SpeechLanguage.Japanese, "a", .22f, .035f, "long A retains the vowel pose"), Step(SpeechLanguage.Japanese, "sz", .08f, .03f), Step(SpeechLanguage.Japanese, "a", .09f, .04f), Step(SpeechLanguage.Japanese, "n_alveolar", .1f, .06f), Step(SpeechLanguage.Japanese, "o", .11f, .035f), Step(SpeechLanguage.Japanese, "shj", .08f, .03f), Step(SpeechLanguage.Japanese, "i", .22f, .035f, "long I retains the vowel pose"), Step(SpeechLanguage.Japanese, "sz", .08f, .03f), Step(SpeechLanguage.Japanese, "a", .09f, .04f), Step(SpeechLanguage.Japanese, "n_alveolar", .1f, 0f)),
                Sequence("ja_devoiced", "JA: です / すき", SpeechLanguage.Japanese, "です / すき",
                    Step(SpeechLanguage.Japanese, "tdn", .06f, .025f), Step(SpeechLanguage.Japanese, "e", .08f, .025f), Step(SpeechLanguage.Japanese, "sz", .09f, .025f), Step(SpeechLanguage.Japanese, "u", .045f, .025f, "quiet vowel; do not snap to silence"), Step(SpeechLanguage.Japanese, "sz", .08f, .025f), Step(SpeechLanguage.Japanese, "u", .045f, .025f, "quiet vowel"), Step(SpeechLanguage.Japanese, "kg", .07f, .025f), Step(SpeechLanguage.Japanese, "i", .09f, 0f)),
                Sequence("ja_loan_ti_di_tu_du", "JA: ティ/ディ トゥ/ドゥ", SpeechLanguage.Japanese, "ティ / ディ / トゥ / ドゥ",
                    Step(SpeechLanguage.Japanese, "tdn", .08f, .025f), Step(SpeechLanguage.Japanese, "i", .1f, .04f), Step(SpeechLanguage.Japanese, "tdn", .08f, .025f), Step(SpeechLanguage.Japanese, "i", .1f, .04f), Step(SpeechLanguage.Japanese, "tdn", .08f, .025f), Step(SpeechLanguage.Japanese, "u", .1f, .04f), Step(SpeechLanguage.Japanese, "tdn", .08f, .025f), Step(SpeechLanguage.Japanese, "u", .1f, 0f)),
                Sequence("ja_loan_v_w", "JA: ヴ / ウィウェウォ", SpeechLanguage.Japanese, "ヴ / ウィ / ウェ / ウォ",
                    Step(SpeechLanguage.Japanese, "v", .11f, .04f), Step(SpeechLanguage.Japanese, "u", .09f, .05f), Step(SpeechLanguage.Japanese, "w", .07f, .025f), Step(SpeechLanguage.Japanese, "i", .1f, .04f), Step(SpeechLanguage.Japanese, "w", .07f, .025f), Step(SpeechLanguage.Japanese, "e", .1f, .04f), Step(SpeechLanguage.Japanese, "w", .07f, .025f), Step(SpeechLanguage.Japanese, "o", .1f, 0f)),
                Sequence("ja_code_switch", "JA/EN: フル LOOP", SpeechLanguage.Japanese, "フル loop",
                    Step(SpeechLanguage.Japanese, "fu", .09f, .03f), Step(SpeechLanguage.Japanese, "u", .09f, .05f), Step(SpeechLanguage.English, "l", .08f, .04f), Step(SpeechLanguage.English, "u", .12f, .04f), Step(SpeechLanguage.English, "pbm", .09f, 0f))
            };
        }

        public static bool TryParseLanguage(string value, out SpeechLanguage language, out string diagnostic)
        {
            if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "english", StringComparison.OrdinalIgnoreCase))
            {
                language = SpeechLanguage.English;
                diagnostic = string.Empty;
                return true;
            }
            if (string.Equals(value, "ja", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "japanese", StringComparison.OrdinalIgnoreCase) || value == "日本語")
            {
                language = SpeechLanguage.Japanese;
                diagnostic = string.Empty;
                return true;
            }
            language = default;
            diagnostic = "UNKNOWN_SPEECH_LANGUAGE: '" + value + "' has no configured pose set.";
            return false;
        }

        public static bool TryFindPose(IEnumerable<SpeechPoseProfile> poses, SpeechLanguage language, string poseId,
            out SpeechPoseProfile pose, out string diagnostic)
        {
            if (poses != null)
                foreach (var candidate in poses)
                    if (candidate != null && candidate.Language == language &&
                        string.Equals(candidate.Id, poseId, StringComparison.Ordinal))
                    {
                        pose = candidate;
                        diagnostic = string.Empty;
                        return true;
                    }
            pose = null;
            diagnostic = "UNKNOWN_SPEECH_POSE: " + language + "/" + poseId + " is not configured; no fallback applied.";
            return false;
        }

        static SpeechPoseProfile Pose(SpeechLanguage language, string id, string label,
            SpeechContactBehavior contact, string notes, params SpeechTargetContribution[] targets)
        {
            return new SpeechPoseProfile
            {
                Language = language,
                Id = id,
                Label = label,
                Contact = contact,
                ContactNotes = notes,
                Targets = targets ?? Array.Empty<SpeechTargetContribution>()
            };
        }

        static SpeechTargetContribution Target(string shapeName, float weight = 1f) =>
            new SpeechTargetContribution(shapeName, weight);

        static SpeechReviewStep Step(SpeechLanguage language, string poseId, float hold, float transition,
            string context = "") => new SpeechReviewStep(language, poseId, hold, transition, context);

        static SpeechReviewSequence Sequence(string id, string label, SpeechLanguage language, string text,
            params SpeechReviewStep[] steps) => new SpeechReviewSequence
        {
            Id = id,
            Label = label,
            Language = language,
            ReviewText = text,
            Steps = steps
        };
    }
}
