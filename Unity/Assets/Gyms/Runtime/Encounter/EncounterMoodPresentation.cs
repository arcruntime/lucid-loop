using UnityEngine;

namespace LucidLoop.Gyms
{
    // Both moods use the same shader features. ClubLighting writes its normal pulse
    // during Update; we scale that frame's result during LateUpdate.
    public sealed class EncounterMoodPresentation : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public EncounterVoiceController Voice;
        public ClubLighting Club;
        public AudioClip AggressiveLoop;
        public AudioClip IntimateLoop;
        [Range(0, 1)] public float MusicVolume = .32f;
        [Range(0, 1)] public float DialogueMusicMultiplier = .2f;
        [Min(.1f)] public float MoodTransitionSeconds = 2.5f;

        EncounterCoordinator bound;
        AudioSource aggressiveMusic, intimateMusic;
        Light[] lights;
        Color[] originalColors;
        float[] originalIntensities;
        float intimacy, targetIntimacy, musicDuck = 1;
        bool paused;

        void Awake()
        {
            aggressiveMusic = CreateSource(); intimateMusic = CreateSource();
        }

        AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 0;
            source.volume = 0; source.priority = 160;
            return source;
        }

        void Start()
        {
            if (!Coordinator) Coordinator = GetComponent<EncounterCoordinator>();
            if (!Voice) Voice = GetComponent<EncounterVoiceController>();
            Bind(); CaptureLights(); StartMusic();
        }

        void OnEnable()
        {
            // Awake has already created our two sources; Start performs first binding.
            if (lights != null) { Bind(); StartMusic(); }
        }

        void Bind()
        {
            if (bound == Coordinator) return;
            Unbind(); bound = Coordinator;
            if (!bound) return;
            bound.MoodChanged += OnMood;
            bound.PauseChanged += OnPause;
            OnMood(bound.State.Mood);
        }

        void CaptureLights()
        {
            lights = Club && Club.Lights != null ? (Light[])Club.Lights.Clone() : new Light[0];
            originalColors = new Color[lights.Length]; originalIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                if (lights[i]) { originalColors[i] = lights[i].color; originalIntensities[i] = lights[i].intensity; }
        }

        void OnMood(string mood)
        {
            // Unknown state retains the last valid mood. Initial demo mood is aggressive.
            if (mood == "Intimate") targetIntimacy = 1;
            else if (mood == "Aggressive") targetIntimacy = 0;
        }

        void OnPause(bool value)
        {
            paused = value;
            if (paused) { aggressiveMusic.Pause(); intimateMusic.Pause(); }
            else { aggressiveMusic.UnPause(); intimateMusic.UnPause(); }
        }

        void StartMusic()
        {
            aggressiveMusic.clip = AggressiveLoop; intimateMusic.clip = IntimateLoop;
            double start = AudioSettings.dspTime + .15;
            if (AggressiveLoop) aggressiveMusic.PlayScheduled(start);
            if (IntimateLoop) intimateMusic.PlayScheduled(start);
        }

        void LateUpdate()
        {
            Bind();
            // ClubLighting still writes its pulse while paused. Keep applying the mood
            // multiplier, but freeze the transition itself until the encounter resumes.
            if (!paused)
            {
                intimacy = Mathf.MoveTowards(intimacy, targetIntimacy,
                    Time.unscaledDeltaTime / Mathf.Max(.1f, MoodTransitionSeconds));
                bool dialogue = Voice && (Voice.IsReady || Voice.IsConnecting || Voice.IsClosing);
                musicDuck = Mathf.MoveTowards(musicDuck, dialogue ? DialogueMusicMultiplier : 1,
                    Time.unscaledDeltaTime / (dialogue ? .2f : .8f));
            }
            // Linear crossfade avoids the energy rise when both loops share correlated bass.
            aggressiveMusic.volume = MusicVolume * musicDuck * (1 - intimacy);
            intimateMusic.volume = MusicVolume * musicDuck * intimacy;
            if (lights == null) return;
            float scale = Mathf.Lerp(1, .65f, intimacy);
            for (int i = 0; i < lights.Length; i++)
            {
                if (!lights[i]) continue;
                Color aggressive = i % 2 == 0 ? new Color(1, .025f, .10f) : new Color(.45f, .06f, 1);
                Color intimate = i % 2 == 0 ? new Color(1, .24f, .42f) : new Color(1, .49f, .28f);
                lights[i].color = Color.Lerp(aggressive, intimate, intimacy);
                // Only multiply when ClubLighting actually rewrote this same light this frame.
                bool pulsed = Club && Club.isActiveAndEnabled && Club.Lights != null &&
                    i < Club.Lights.Length && Club.Lights[i] == lights[i];
                lights[i].intensity = (pulsed ? lights[i].intensity : originalIntensities[i]) * scale;
            }
        }

        void Unbind()
        {
            if (bound) { bound.MoodChanged -= OnMood; bound.PauseChanged -= OnPause; }
            bound = null;
        }

        void OnDisable()
        {
            Unbind(); paused = false;
            if (aggressiveMusic) aggressiveMusic.Stop();
            if (intimateMusic) intimateMusic.Stop();
            if (lights == null) return;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i]) { lights[i].color = originalColors[i]; lights[i].intensity = originalIntensities[i]; }
        }
    }
}
