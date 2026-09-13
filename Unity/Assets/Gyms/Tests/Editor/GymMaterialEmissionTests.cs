using LucidLoop.Gyms.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LucidLoop.Gyms.Tests
{
    public sealed class GymMaterialEmissionTests
    {
        [TestCase("Amber glow")]
        [TestCase("Cyan glow")]
        [TestCase("Magenta glow")]
        [TestCase("Screen purple")]
        [TestCase("Bottle0")]
        [TestCase("Bottle1")]
        [TestCase("Bottle2")]
        public void AuthoredGlowSurvivesForcedReimport(string name)
        {
            string path = "Assets/Gyms/Generated/" + name + ".mat";
            AssertEmission(AssetDatabase.LoadAssetAtPath<Material>(path));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssertEmission(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        [Test]
        public void GeneratorCanEnableAndClearEmissionThroughUrpValidation()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                GymMaterialEmission.Configure(material, new Color(2, .5f, .2f));
                AssertEmission(material);
                GymMaterialEmission.Configure(material, Color.black);
                Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.False);
                Assert.That(material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive, Is.EqualTo((MaterialGlobalIlluminationFlags)0));
            }
            finally { Object.DestroyImmediate(material); }
        }

        static void AssertEmission(Material material)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(0));
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.BakedEmissive,
                Is.EqualTo(MaterialGlobalIlluminationFlags.BakedEmissive));
            Assert.That(material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                Is.EqualTo((MaterialGlobalIlluminationFlags)0));
        }
    }
}
