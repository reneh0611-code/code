using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class NPCAppearanceRandomizer : MonoBehaviour
    {
        private static readonly Color[] Upper =
        {
            new(0.03f,0.03f,0.04f),
            new(0.10f,0.13f,0.18f),
            new(0.13f,0.22f,0.34f),
            new(0.18f,0.10f,0.09f),
            new(0.10f,0.22f,0.14f),
            new(0.34f,0.32f,0.30f),
            new(0.78f,0.78f,0.76f)
        };

        private static readonly Color[] Lower =
        {
            new(0.94f,0.94f,0.91f),
            new(0.04f,0.04f,0.05f),
            new(0.18f,0.19f,0.21f),
            new(0.15f,0.24f,0.36f),
            new(0.47f,0.41f,0.31f)
        };

        private static readonly Color[] Shoes =
        {
            new(0.03f,0.03f,0.035f),
            new(0.90f,0.90f,0.88f),
            new(0.17f,0.17f,0.18f)
        };

        private void Start()
        {
            ApplyTint();
        }

        private void ApplyTint()
        {
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r != null)
                .ToArray();

            Color upper = Upper[Random.Range(0, Upper.Length)];
            Color lower = Lower[Random.Range(0, Lower.Length)];
            Color shoes = Shoes[Random.Range(0, Shoes.Length)];

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                string key = BuildKey(renderer);

                // Absolutely never recolor anything that may contain skin, face, head, hair or hands.
                if (ContainsAny(key,
                    "skin", "face", "head", "hair", "eye", "eyes", "mouth", "teeth",
                    "hand", "hands", "arm", "arms", "body", "torso", "neck"))
                    continue;

                bool upperClothing = ContainsAny(key,
                    "shirt", "hoodie", "sweater", "jacket", "coat", "top", "vest", "pullover", "sweatshirt");

                bool lowerClothing = ContainsAny(key,
                    "pants", "pant", "trouser", "trousers", "jeans", "shorts", "legwear");

                bool footwear = ContainsAny(key,
                    "shoe", "shoes", "boot", "boots", "sneaker", "sneakers", "footwear");

                // Unknown or mixed renderer/material: leave AJ's original material completely untouched.
                if (!upperClothing && !lowerClothing && !footwear)
                    continue;

                Color tint = footwear ? shoes : lowerClothing ? lower : upper;

                Material[] mats = renderer.materials;
                for (int slot = 0; slot < mats.Length; slot++)
                {
                    Material mat = mats[slot];
                    if (mat == null)
                        continue;

                    string materialKey = mat.name.ToLowerInvariant();

                    // Double protection: if an individual material name hints at skin/face, never tint it.
                    if (ContainsAny(materialKey,
                        "skin", "face", "head", "hair", "eye", "mouth", "hand", "body"))
                        continue;

                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", tint);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", tint);
                }

                renderer.materials = mats;
            }
        }

        private static string BuildKey(SkinnedMeshRenderer renderer)
        {
            return (renderer.name + " " +
                    (renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty) + " " +
                    string.Join(" ", renderer.sharedMaterials.Where(m => m != null).Select(m => m.name)))
                .ToLowerInvariant();
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (string token in tokens)
                if (value.Contains(token))
                    return true;
            return false;
        }
    }
}
