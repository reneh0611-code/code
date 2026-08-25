using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    public sealed class NPCAppearanceRandomizer : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool child;
        [SerializeField] private bool addCap;

        private static readonly Color[] ClothesPalette =
        {
            new(0.08f, 0.09f, 0.11f),
            new(0.12f, 0.18f, 0.27f),
            new(0.22f, 0.12f, 0.10f),
            new(0.11f, 0.23f, 0.16f),
            new(0.30f, 0.25f, 0.16f),
            new(0.24f, 0.24f, 0.25f),
            new(0.10f, 0.10f, 0.10f),
            new(0.35f, 0.34f, 0.32f)
        };

        private static readonly Color[] SkinPalette =
        {
            new(0.76f, 0.58f, 0.44f),
            new(0.62f, 0.43f, 0.31f),
            new(0.46f, 0.30f, 0.22f),
            new(0.84f, 0.67f, 0.52f),
            new(0.34f, 0.22f, 0.17f)
        };

        private void Start()
        {
            if (visualRoot == null)
                visualRoot = transform;

            ApplyRandomizedAppearance();
        }

        public void Configure(bool isChild, bool shouldAddCap)
        {
            child = isChild;
            addCap = shouldAddCap;
        }

        private void ApplyRandomizedAppearance()
        {
            Color clothes = ClothesPalette[Random.Range(0, ClothesPalette.Length)];
            Color clothesSecondary = ClothesPalette[Random.Range(0, ClothesPalette.Length)];
            Color skin = SkinPalette[Random.Range(0, SkinPalette.Length)];

            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    if (mat == null)
                        continue;

                    string key = (renderer.name + " " + mat.name).ToLowerInvariant();
                    Color target;

                    if (key.Contains("skin") || key.Contains("head") || key.Contains("face") || key.Contains("hand") || key.Contains("body"))
                        target = skin;
                    else if (key.Contains("shoe") || key.Contains("foot"))
                        target = new Color(0.04f, 0.04f, 0.05f);
                    else
                        target = i % 2 == 0 ? clothes : clothesSecondary;

                    // Preserve any real texture. We only tint the material color.
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", target);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", target);
                }
            }

            if (addCap)
                CreateCap();
        }

        private void CreateCap()
        {
            if (transform.Find("NPC_Cap") != null)
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
            crown.transform.localPosition = Vector3.zero;

            GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brim.name = "Brim";
            brim.transform.SetParent(capRoot.transform, false);
            brim.transform.localScale = new Vector3(headWidth * 1.25f, 0.018f, headWidth * 0.75f);
            brim.transform.localPosition = new Vector3(0f, -0.02f, headWidth * 0.70f);

            Material capMaterial = new(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            capMaterial.name = "NPC_BlackCap_Runtime";
            if (capMaterial.HasProperty("_BaseColor")) capMaterial.SetColor("_BaseColor", new Color(0.025f, 0.025f, 0.03f));
            if (capMaterial.HasProperty("_Color")) capMaterial.SetColor("_Color", new Color(0.025f, 0.025f, 0.03f));

            crown.GetComponent<Renderer>().material = capMaterial;
            brim.GetComponent<Renderer>().material = capMaterial;

            Object.Destroy(crown.GetComponent<Collider>());
            Object.Destroy(brim.GetComponent<Collider>());
        }
    }
}
