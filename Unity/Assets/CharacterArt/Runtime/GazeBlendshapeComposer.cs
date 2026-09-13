using System;
using System.Collections.Generic;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    /// <summary>
    /// Converts the authored gaze vector (degrees) to the canonical directional eye morphs.
    /// A 20 degree look is calibrated as full (100) morph travel.
    /// </summary>
    public static class GazeBlendshapeComposer
    {
        public const float DegreesAtFullWeight = 20f;

        static readonly string[] HorizontalShapes =
        {
            "eyeLookInLeft", "eyeLookOutLeft", "eyeLookInRight", "eyeLookOutRight"
        };

        static readonly string[] VerticalShapes =
        {
            "eyeLookUpLeft", "eyeLookDownLeft", "eyeLookUpRight", "eyeLookDownRight"
        };

        public static void AddSynthesizedGaze(
            IDictionary<string, float> pose,
            IReadOnlyDictionary<string, float> authoredExpression,
            float expressionIntensity,
            Vector2 gazeDegrees,
            bool synthesizeLeft,
            bool synthesizeRight)
        {
            if (pose == null) return;
            var strength = Mathf.Clamp01(expressionIntensity);
            if (strength <= 0f) return;

            if (!OwnsAny(authoredExpression, HorizontalShapes))
            {
                var horizontal = Mathf.Clamp01(Mathf.Abs(gazeDegrees.x) / DegreesAtFullWeight) * 100f * strength;
                if (horizontal > 0f)
                {
                    if (gazeDegrees.x > 0f)
                    {
                        if (synthesizeLeft) pose["eyeLookInLeft"] = horizontal;
                        if (synthesizeRight) pose["eyeLookOutRight"] = horizontal;
                    }
                    else
                    {
                        if (synthesizeLeft) pose["eyeLookOutLeft"] = horizontal;
                        if (synthesizeRight) pose["eyeLookInRight"] = horizontal;
                    }
                }
            }

            if (OwnsAny(authoredExpression, VerticalShapes)) return;
            var vertical = Mathf.Clamp01(Mathf.Abs(gazeDegrees.y) / DegreesAtFullWeight) * 100f * strength;
            if (vertical <= 0f) return;
            var direction = gazeDegrees.y > 0f ? "eyeLookUp" : "eyeLookDown";
            if (synthesizeLeft) pose[direction + "Left"] = vertical;
            if (synthesizeRight) pose[direction + "Right"] = vertical;
        }

        static bool OwnsAny(IReadOnlyDictionary<string, float> expression, string[] names)
        {
            if (expression == null) return false;
            foreach (var name in names)
                if (expression.ContainsKey(name)) return true;
            return false;
        }
    }
}
