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
                .OrderBy(r => r.bounds.center.y)
                .ToArray();

            if (renderers.Length == 0)
                return;

            Color upper = Upper[Random.Range(0, Upper.Length)];
            Color lower = Lower[Random.Range(0, Lower.Length)];
            Color shoes = Shoes[Random.Range(0, Shoes.Length)];

            Bounds full = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                full.Encapsulate(renderers[i].bounds);

            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                string key = BuildKey(renderer);

                if (IsSkinFaceHair(key))
                    continue;

                Bounds b = renderer.bounds;
                float center01 = Mathf.InverseLerp(full.min.y, full.max.y, b.center.y);

                Color tint;
                if (ContainsAny(key, "shoe", "boot", "sneaker", "footwear"))
                    tint = shoes;
                else if (ContainsAny(key, "pant", "trouser", "jeans", "short", "lower"))
                    tint = lower;
                else if (ContainsAny(key, "shirt", "hoodie", "sweater", "jacket", "coat", "top", "upper", "vest", "pullover"))
                    tint = upper;
                else
                {
                    // Fallback for neutral renderer names: use vertical position only.
                    // Lowest clothing piece = shoes, middle-lower = pants, middle-upper = top.
                    // The very highest renderer is preserved because it is usually hair/head/accessory.
                    if (center01 < 0.22f)
                        tint = shoes;
                    else if (center01 < 0.52f)
                        tint = lower;
                    else if (center01 < 0.86f)
                        tint = upper;
                    else
                        continue;
                }

                // renderer.materials creates per-NPC material instances.
                // Original AJ textures remain assigned, only the material tint is changed.
                Material[] mats = renderer.materials;
                for (int slot = 0; slot < mats.Length; slot++)
                {
                    Material mat = mats[slot];
                    if (mat == null)
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

        private static bool IsSkinFaceHair(string key)
        {
            return ContainsAny(key,
                "skin", "face", "head", "hair", "eye", "eyes", "mouth", "teeth", "hand");
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
