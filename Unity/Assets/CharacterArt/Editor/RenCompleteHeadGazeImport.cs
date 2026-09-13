using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenCompleteHeadGazeCalibration
    {
        public string side, rotationObject;
        public Vector3 axisX, axisY, axisZ, markerLengths;
        public Quaternion restLocalRotation;
        public float handedness, maximumAxisDot;
    }

    /// <summary>Derives local rotation conjugation from actual imported calibration EMPTY nodes.</summary>
    public static class RenCompleteHeadGazeImport
    {
        public static RenCompleteHeadGazeCalibration[] Configure(GameObject source, RenCompleteHeadRig rig)
        {
            var all = source.GetComponentsInChildren<Transform>(true);
            var calibrations = new List<RenCompleteHeadGazeCalibration>(); var eyes = new List<RenCompleteHeadRig.Eye>();
            foreach (var side in new[] { "L", "R" })
            {
                var rotation = One(all, "Ren_GazeRotate_" + side);
                var ellipsoid = One(all, "Ren_GazeEllipsoid_" + side);
                var iris = One(all, "Ren_Eye_" + side + "_Iris");
                if (rotation.parent != ellipsoid || !iris.IsChildOf(rotation))
                    throw new InvalidDataException("The authored ellipsoid/rotation/iris hierarchy changed for eye " + side);
                var basis = new Vector3[3]; var lengths = Vector3.zero;
                for (var i = 0; i < 3; i++)
                {
                    var marker = One(all, "Ren_GazeAxis_" + side + "_" + "XYZ"[i]);
                    if (!marker.IsChildOf(rotation)) throw new InvalidDataException("Eye axis marker moved outside its rotation frame.");
                    basis[i] = rotation.InverseTransformPoint(marker.position); lengths[i] = basis[i].magnitude;
                    if (lengths[i] < .00001f) throw new InvalidDataException("Degenerate eye axis marker.");
                    basis[i] /= lengths[i];
                }
                var maxDot = Mathf.Max(Mathf.Abs(Vector3.Dot(basis[0], basis[1])), Mathf.Abs(Vector3.Dot(basis[0], basis[2])), Mathf.Abs(Vector3.Dot(basis[1], basis[2])));
                var determinant = Vector3.Dot(Vector3.Cross(basis[1], basis[2]), basis[0]);
                if (maxDot > .0001f || Mathf.Abs(Mathf.Abs(determinant) - 1) > .0001f ||
                    Mathf.Abs(lengths.x - lengths.y) > .0001f * lengths.x || Mathf.Abs(lengths.x - lengths.z) > .0001f * lengths.x)
                    throw new InvalidDataException("Imported eye calibration is not a uniformly scaled orthonormal basis.");
                var handedness = Mathf.Sign(determinant);
                eyes.Add(new RenCompleteHeadRig.Eye { Side = side, Rotation = rotation, RestLocalRotation = rotation.localRotation,
                    PitchAxis = basis[1] * handedness, YawAxis = basis[2] * handedness });
                calibrations.Add(new RenCompleteHeadGazeCalibration { side = side, rotationObject = rotation.name,
                    axisX = basis[0], axisY = basis[1], axisZ = basis[2], markerLengths = lengths,
                    restLocalRotation = rotation.localRotation, handedness = handedness, maximumAxisDot = maxDot });
            }
            foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.sharedMesh)
                    for (var i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    {
                        var shape = skin.sharedMesh.GetBlendShapeName(i);
                        if (new[] { "gazeLeft", "gazeRight", "gazeUp", "gazeDown" }.Any(name => RenFaceStudyController.ShapeMatches(shape, name)))
                            throw new InvalidDataException("New source still contains a removed additive gaze key: " + skin.name + "/" + shape);
                    }
            rig.Eyes = eyes.ToArray(); return calibrations.ToArray();
        }
        public static Transform One(Transform[] transforms, string name)
        {
            var found = transforms.Where(item => item.name == name).ToArray();
            if (found.Length != 1) throw new InvalidDataException("Expected one imported node named " + name + ", found " + found.Length);
            return found[0];
        }
    }
}
