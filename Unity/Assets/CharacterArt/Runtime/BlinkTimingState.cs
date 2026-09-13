using System;

namespace LucidLoop.CharacterArt
{
    public enum BlinkPhase
    {
        Waiting,
        Closing,
        Holding,
        Opening
    }

    [Flags]
    public enum BlinkEyes
    {
        None = 0,
        Left = 1,
        Right = 2,
        Both = Left | Right
    }

    public readonly struct BlinkFrame
    {
        public static readonly BlinkFrame Open = new BlinkFrame(0f, 0f);

        public readonly float Left;
        public readonly float Right;

        public BlinkFrame(float left, float right)
        {
            Left = Clamp01(left);
            Right = Clamp01(right);
        }

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    public sealed class BlinkTimingState
    {
        public const float CloseSeconds = .070f;
        public const float HoldSeconds = .035f;
        public const float OpenSeconds = .110f;
        const float TransitionEpsilon = .000001f;

        readonly Func<float> nextInterval;
        float phaseTime;
        float timeUntilAutomaticBlink;
        bool automaticEnabled = true;
        BlinkEyes activeEyes;

        public BlinkPhase Phase { get; private set; } = BlinkPhase.Waiting;
        public float TimeUntilAutomaticBlink => timeUntilAutomaticBlink;

        public bool AutomaticEnabled
        {
            get => automaticEnabled;
            set
            {
                if (automaticEnabled == value) return;
                automaticEnabled = value;
                ResetToOpen();
            }
        }

        public BlinkTimingState(Func<float> nextInterval)
        {
            this.nextInterval = nextInterval ?? throw new ArgumentNullException(nameof(nextInterval));
            ScheduleNext();
        }

        public void Trigger(BlinkEyes eyes = BlinkEyes.Both)
        {
            if (eyes == BlinkEyes.None) return;
            activeEyes = eyes;
            Phase = BlinkPhase.Closing;
            phaseTime = 0f;
        }

        public BlinkFrame Advance(float deltaSeconds)
        {
            var remaining = Math.Max(0f, deltaSeconds);
            for (var transitions = 0; transitions < 4096; transitions++)
            {
                if (Phase == BlinkPhase.Waiting)
                {
                    if (!automaticEnabled) return BlinkFrame.Open;
                    if (remaining + TransitionEpsilon < timeUntilAutomaticBlink)
                    {
                        timeUntilAutomaticBlink -= remaining;
                        return BlinkFrame.Open;
                    }

                    remaining -= timeUntilAutomaticBlink;
                    Trigger(BlinkEyes.Both);
                    if (remaining <= 0f) return CurrentFrame();
                    continue;
                }

                var duration = Duration(Phase);
                var untilTransition = duration - phaseTime;
                if (remaining + TransitionEpsilon < untilTransition)
                {
                    phaseTime += remaining;
                    return CurrentFrame();
                }

                remaining -= untilTransition;
                phaseTime = 0f;
                MoveToNextPhase();
                if (remaining <= 0f) return CurrentFrame();
            }

            throw new InvalidOperationException("Blink timing exceeded the transition safety limit.");
        }

        void MoveToNextPhase()
        {
            switch (Phase)
            {
                case BlinkPhase.Closing:
                    Phase = BlinkPhase.Holding;
                    break;
                case BlinkPhase.Holding:
                    Phase = BlinkPhase.Opening;
                    break;
                case BlinkPhase.Opening:
                    ResetToOpen();
                    break;
            }
        }

        void ResetToOpen()
        {
            Phase = BlinkPhase.Waiting;
            phaseTime = 0f;
            activeEyes = BlinkEyes.None;
            ScheduleNext();
        }

        void ScheduleNext()
        {
            timeUntilAutomaticBlink = Math.Max(.01f, nextInterval());
        }

        BlinkFrame CurrentFrame()
        {
            float closure;
            switch (Phase)
            {
                case BlinkPhase.Closing:
                    closure = phaseTime / CloseSeconds;
                    break;
                case BlinkPhase.Holding:
                    closure = 1f;
                    break;
                case BlinkPhase.Opening:
                    closure = 1f - phaseTime / OpenSeconds;
                    break;
                default:
                    closure = 0f;
                    break;
            }

            return new BlinkFrame(
                (activeEyes & BlinkEyes.Left) != 0 ? closure : 0f,
                (activeEyes & BlinkEyes.Right) != 0 ? closure : 0f);
        }

        static float Duration(BlinkPhase phase)
        {
            switch (phase)
            {
                case BlinkPhase.Closing: return CloseSeconds;
                case BlinkPhase.Holding: return HoldSeconds;
                case BlinkPhase.Opening: return OpenSeconds;
                default: return 0f;
            }
        }
    }
}
