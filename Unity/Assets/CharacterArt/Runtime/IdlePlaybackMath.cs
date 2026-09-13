using System;

namespace LucidLoop.CharacterArt
{
    public static class IdlePlaybackMath
    {
        public static double WrapTime(double time, double clipLength)
        {
            if (clipLength <= 0d || double.IsNaN(time) || double.IsInfinity(time)) return 0d;
            var wrapped = time % clipLength;
            return wrapped < 0d ? wrapped + clipLength : wrapped;
        }
    }
}
