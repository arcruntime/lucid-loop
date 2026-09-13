// Ported from HeadAudio modules/vadgate.mjs (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.

using System;

namespace SplatterfaceGames.LipSync
{
    /// <summary>Voice activity detection using a simple gate model. Ported 1:1.</summary>
    internal sealed class VadGate
    {
        private readonly double _activeLE;
        private readonly int _activeFrames;
        private readonly double _inactiveLE;
        private readonly int _inactiveFrames;

        private int _active;
        private int _inactive;
        private int _preActive;
        private int _preInactive;

        public VadGate(double vadGateActiveDb = -40, double vadGateActiveMs = 10,
            double vadGateInactiveDb = -50, double vadGateInactiveMs = 10)
        {
            _activeLE = vadGateActiveDb / 10;
            // JS Math.round == half away from zero
            _activeFrames = Math.Max(1, (int)Math.Round(
                vadGateActiveMs * ((double)Parameters.AudioSampleRate / Parameters.MfccSamplesHop) / 1000,
                MidpointRounding.AwayFromZero));
            _inactiveLE = vadGateInactiveDb / 10;
            _inactiveFrames = Math.Max(1, (int)Math.Round(
                vadGateInactiveMs * ((double)Parameters.AudioSampleRate / Parameters.MfccSamplesHop) / 1000,
                MidpointRounding.AwayFromZero));

            _active = 0;
            _inactive = 1;
            _preActive = 0;
            _preInactive = 0;
        }

        public void Reset()
        {
            _active = 0;
            _inactive = 1;
            _preActive = 0;
            _preInactive = 0;
        }

        /// <summary>Update VAD statistics for a frame; returns (active, inactive) flags.</summary>
        public (int active, int inactive) Process(double le)
        {
            if (_active != 0)
            {
                _active++;
                if (le < _inactiveLE)
                {
                    _preInactive++;
                    if (_preInactive >= _inactiveFrames)
                    {
                        _active = 0;
                        _inactive = 1;
                        _preActive = 0;
                    }
                }
                else
                {
                    if (_preInactive > 0) _preInactive--;
                }
            }
            else
            {
                _inactive++;
                if (le > _activeLE)
                {
                    _preActive++;
                    if (_preActive >= _activeFrames)
                    {
                        _active = 1;
                        _inactive = 0;
                        _preInactive = 0;
                    }
                }
                else
                {
                    if (_preActive > 0) _preActive--;
                }
            }
            return (_active, _inactive);
        }
    }
}
