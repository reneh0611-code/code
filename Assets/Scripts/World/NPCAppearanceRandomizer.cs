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
            new(0.34f,0.32f,0.30f)
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

            if (renderers.Length == 0)
                return;

            Bounds full = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                full.Encapsulate(renderers[i].bounds);

            Color upper = Upper[Random.Range(0, Upper.Length)];
            Color lower = Lower[Random.Range(0, Lower.Length)];
            Color shoes = Shoes[Random.Range(0, Shoes.Length)];

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Bounds b = renderer.bounds;
                float height = Mathf.Max(0.001f, full.size.y);
                float center01 = Mathf.InverseLerp(full.min.y, full.max.y, b.center.y);
                float size01 = b.size.y / height;

                string key = (renderer.name + " " +
                    (renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty) + " " +
                    string.Join(" ", renderer.sharedMaterials.Where(m => m != null).Select(m => m.name))).ToLowerInvariant();

                // Preserve skin, face, hair and any renderer spanning most of the body.
                if (ContainsAny(key, "skin", "face", "head", "hair", "eye", "mouth", "hand", "body") || size01 > 0.58f)
                    continue;

                Color tint;
                if (ContainsAny(key, "shoe", "boot", "sneaker") || (center01 < 0.18f && size01 < 0.30f))
                    tint = shoes;
                else if (ContainsAny(key, "pant", "trouser", "jeans", "short", "lower") || center01 < 0.48f)
                    tint = lower;
                else
                    tint = upper;

                Material[] mats = renderer.sharedMaterials;
                for (int slot = 0; slot < mats.Length; slot++)
                {
                    Material source = mats[slot];
                    if (source == null)
                        continue;

                    MaterialPropertyBlock block = new();
                    renderer.GetPropertyBlock(block, slot);
                    if (source.HasProperty("_BaseColor")) block.SetColor("_BaseColor", tint);
                    if (source.HasProperty("_Color")) block.SetColor("_Color", tint);
                    renderer.SetPropertyBlock(block, slot);
                }
            }
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
