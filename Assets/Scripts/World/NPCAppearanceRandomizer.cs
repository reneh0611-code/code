using System.Linq;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class NPCAppearanceRandomizer : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool child;
        [SerializeField] private bool addCap;

        private static readonly Color[] UpperPalette =
        {
            new(0.02f, 0.02f, 0.025f), // black
            new(0.10f, 0.12f, 0.16f),
            new(0.10f, 0.18f, 0.32f),
            new(0.22f, 0.10f, 0.09f),
            new(0.08f, 0.22f, 0.14f),
            new(0.36f, 0.34f, 0.31f)
        };

        private static readonly Color[] PantsPalette =
        {
            new(0.94f, 0.94f, 0.92f), // white/off-white
            new(0.04f, 0.04f, 0.05f),
            new(0.18f, 0.19f, 0.21f),
            new(0.16f, 0.25f, 0.37f),
            new(0.48f, 0.42f, 0.31f)
        };

        private static readonly Color[] ShoePalette =
        {
            new(0.025f, 0.025f, 0.03f),
            new(0.92f, 0.92f, 0.90f),
            new(0.17f, 0.17f, 0.18f)
        };

        private enum PartType
        {
            Preserve,
            Upper,
            Pants,
            Shoes,
            Backpack
        }

        private void Start()
        {
            if (visualRoot == null)
                visualRoot = transform;

            HideBackpackObjectsAndRenderers();
            ApplyRandomizedAppearance();
        }

        public void Configure(bool isChild, bool shouldAddCap)
        {
            child = isChild;
            addCap = shouldAddCap;
        }

        private void ApplyRandomizedAppearance()
        {
            Color upper = UpperPalette[Random.Range(0, UpperPalette.Length)];
            Color pants = PantsPalette[Random.Range(0, PantsPalette.Length)];
            Color shoes = ShoePalette[Random.Range(0, ShoePalette.Length)];

            SkinnedMeshRenderer[] renderers = visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r != null && r.enabled)
                .ToArray();

            if (renderers.Length == 0)
                return;

            Bounds fullBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                fullBounds.Encapsulate(renderers[i].bounds);

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                PartType part = ClassifyRenderer(renderer, fullBounds);

                if (part == PartType.Backpack)
                {
                    renderer.enabled = false;
                    continue;
                }

                if (part == PartType.Preserve)
                    continue;

                Color tint = part == PartType.Upper ? upper : part == PartType.Pants ? pants : shoes;
                Material[] shared = renderer.sharedMaterials;

                for (int slot = 0; slot < shared.Length; slot++)
                {
                    Material source = shared[slot];
                    if (source == null)
                        continue;

                    // Property blocks preserve AJ's original texture; only the material tint changes per NPC.
                    MaterialPropertyBlock block = new();
                    renderer.GetPropertyBlock(block, slot);

                    if (source.HasProperty("_BaseColor"))
                        block.SetColor("_BaseColor", tint);
                    if (source.HasProperty("_Color"))
                        block.SetColor("_Color", tint);

                    renderer.SetPropertyBlock(block, slot);
                }
            }

            if (addCap)
                CreateCap();
        }

        private static PartType ClassifyRenderer(SkinnedMeshRenderer renderer, Bounds fullBounds)
        {
            string materialNames = string.Join(" ", renderer.sharedMaterials.Where(m => m != null).Select(m => m.name));
            string meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty;
            string key = (renderer.name + " " + meshName + " " + materialNames).ToLowerInvariant();

            if (IsBackpackRenderer(renderer, key))
                return PartType.Backpack;

            if (ContainsAny(key, "skin", "face", "head", "hair", "eye", "mouth", "body", "hand", "arm"))
                return PartType.Preserve;

            if (ContainsAny(key, "shirt", "hoodie", "sweater", "jacket", "coat", "top", "torso", "upper", "vest", "pullover"))
                return PartType.Upper;

            if (ContainsAny(key, "pants", "pant", "trouser", "jeans", "shorts", "lower", "legwear"))
                return PartType.Pants;

            if (ContainsAny(key, "shoe", "sneaker", "boot", "footwear"))
                return PartType.Shoes;

            // Fallback for neutral Mixamo mesh names. Because AJ has four separate textured
            // SkinnedMeshRenderers, their vertical location is enough to separate clothing
            // without ever touching a renderer that spans most of the body.
            float totalHeight = Mathf.Max(0.001f, fullBounds.size.y);
            Bounds b = renderer.bounds;
            float center01 = Mathf.InverseLerp(fullBounds.min.y, fullBounds.max.y, b.center.y);
            float height01 = b.size.y / totalHeight;

            // A renderer spanning most of the character is almost certainly body/skin.
            if (height01 > 0.58f)
                return PartType.Preserve;

            if (center01 < 0.18f && height01 < 0.28f)
                return PartType.Shoes;

            if (center01 < 0.48f)
                return PartType.Pants;

            if (center01 < 0.86f)
                return PartType.Upper;

            return PartType.Preserve;
        }

        private void HideBackpackObjectsAndRenderers()
        {
            foreach (Transform t in visualRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == visualRoot)
                    continue;

                if (IsBackpackToken(t.name))
                    t.gameObject.SetActive(false);
            }

            foreach (SkinnedMeshRenderer renderer in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string materialNames = string.Join(" ", renderer.sharedMaterials.Where(m => m != null).Select(m => m.name));
                string meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty;
                string key = (renderer.name + " " + meshName + " " + materialNames).ToLowerInvariant();

                if (IsBackpackRenderer(renderer, key))
                    renderer.enabled = false;
            }
        }

        private static bool IsBackpackRenderer(SkinnedMeshRenderer renderer, string key)
        {
            if (IsBackpackToken(key))
                return true;

            Transform[] bones = renderer.bones;
            if (bones == null || bones.Length == 0)
                return false;

            int backpackBones = 0;
            foreach (Transform bone in bones)
            {
                if (bone != null && IsBackpackToken(bone.name))
                    backpackBones++;
            }

            return backpackBones >= 2 && backpackBones >= Mathf.CeilToInt(bones.Length * 0.12f);
        }

        private static bool IsBackpackToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string n = value.ToLowerInvariant();
            return n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack") ||
                   n.Contains("shoulderbag") || n.Contains("back bag") || n == "bag";
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (string token in tokens)
                if (value.Contains(token))
                    return true;
            return false;
        }

        private void CreateCap()
        {
            if (visualRoot.Find("NPC_Cap") != null)
                return;

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && r.enabled)
                .ToArray();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 worldHead = new(bounds.center.x, bounds.max.y - bounds.size.y * 0.04f, bounds.center.z);

            GameObject capRoot = new("NPC_Cap");
            capRoot.transform.SetParent(visualRoot, true);
            capRoot.transform.position = worldHead;

            float headWidth = Mathf.Clamp(bounds.size.x * 0.22f, 0.13f, 0.20f);

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "Crown";
            crown.transform.SetParent(capRoot.transform, false);
            crown.transform.localScale = new Vector3(headWidth, 0.035f, headWidth);

            GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brim.name = "Brim";
            brim.transform.SetParent(capRoot.transform, false);
            brim.transform.localScale = new Vector3(headWidth * 1.25f, 0.018f, headWidth * 0.75f);
            brim.transform.localPosition = new Vector3(0f, -0.02f, headWidth * 0.70f);

            Material capMaterial = new(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            capMaterial.name = "NPC_BlackCap_Runtime";
            Color black = new(0.02f, 0.02f, 0.025f);
            if (capMaterial.HasProperty("_BaseColor")) capMaterial.SetColor("_BaseColor", black);
            if (capMaterial.HasProperty("_Color")) capMaterial.SetColor("_Color", black);

            crown.GetComponent<Renderer>().material = capMaterial;
            brim.GetComponent<Renderer>().material = capMaterial;
            Object.Destroy(crown.GetComponent<Collider>());
            Object.Destroy(brim.GetComponent<Collider>());
        }
    }
}
