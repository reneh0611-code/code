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
            new(0.03f, 0.03f, 0.04f), // black
            new(0.10f, 0.12f, 0.16f), // charcoal
            new(0.12f, 0.20f, 0.34f), // navy
            new(0.20f, 0.12f, 0.10f), // burgundy
            new(0.10f, 0.24f, 0.16f), // green
            new(0.38f, 0.36f, 0.32f)  // warm grey
        };

        private static readonly Color[] PantsPalette =
        {
            new(0.92f, 0.92f, 0.90f), // white/off-white
            new(0.05f, 0.05f, 0.06f), // black
            new(0.20f, 0.21f, 0.23f), // dark grey
            new(0.18f, 0.26f, 0.36f), // denim blue
            new(0.47f, 0.42f, 0.32f)  // beige
        };

        private static readonly Color[] ShoePalette =
        {
            new(0.03f, 0.03f, 0.035f),
            new(0.92f, 0.92f, 0.90f),
            new(0.18f, 0.18f, 0.19f)
        };

        private void Start()
        {
            if (visualRoot == null)
                visualRoot = transform;

            HideBackpackObjects();
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

            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0)
                    continue;

                for (int i = 0; i < shared.Length; i++)
                {
                    Material source = shared[i];
                    if (source == null)
                        continue;

                    string key = (renderer.name + " " + source.name).ToLowerInvariant();

                    // Never recolor skin, face or hair. Those keep AJ's original textured material exactly.
                    if (IsSkinOrHair(key))
                        continue;

                    Color tint;
                    if (IsUpperClothing(key))
                        tint = upper;
                    else if (IsLowerClothing(key))
                        tint = pants;
                    else if (IsShoes(key))
                        tint = shoes;
                    else
                        continue; // ambiguous atlas/material: leave it untouched to protect face/skin textures.

                    MaterialPropertyBlock block = new();
                    renderer.GetPropertyBlock(block, i);

                    if (source.HasProperty("_BaseColor"))
                        block.SetColor("_BaseColor", tint);
                    if (source.HasProperty("_Color"))
                        block.SetColor("_Color", tint);

                    renderer.SetPropertyBlock(block, i);
                }
            }

            if (addCap)
                CreateCap();
        }

        private static bool IsSkinOrHair(string key)
        {
            return key.Contains("skin") || key.Contains("face") || key.Contains("head") ||
                   key.Contains("hand") || key.Contains("arm") || key.Contains("hair") ||
                   key.Contains("eye") || key.Contains("mouth");
        }

        private static bool IsUpperClothing(string key)
        {
            return key.Contains("shirt") || key.Contains("hoodie") || key.Contains("sweater") ||
                   key.Contains("jacket") || key.Contains("coat") || key.Contains("top") ||
                   key.Contains("torso") || key.Contains("upper") || key.Contains("vest");
        }

        private static bool IsLowerClothing(string key)
        {
            return key.Contains("pants") || key.Contains("pant") || key.Contains("trouser") ||
                   key.Contains("jeans") || key.Contains("shorts") || key.Contains("lower") ||
                   key.Contains("legwear");
        }

        private static bool IsShoes(string key)
        {
            return key.Contains("shoe") || key.Contains("sneaker") || key.Contains("boot") || key.Contains("footwear");
        }

        private void HideBackpackObjects()
        {
            foreach (Transform t in visualRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == visualRoot)
                    continue;

                string n = t.name.ToLowerInvariant();
                if (n.Contains("backpack") || n.Contains("back_pack") || n.Contains("rucksack") || n == "bag" || n.Contains("shoulderbag"))
                    t.gameObject.SetActive(false);
            }
        }

        private void CreateCap()
        {
            if (visualRoot.Find("NPC_Cap") != null)
                return;

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
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
