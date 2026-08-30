#if UNITY_6000_5_OR_NEWER
namespace UnityEngine.ProBuilder.AssetIdRemapUtility
{
    // Unity 6.5 removed the legacy non-generic TreeView API used only by
    // ProBuilder's old pre-v4 asset migration window. Core modelling remains
    // available; these no-op shims keep the unrelated migration check isolated.
    static class PackageImporter
    {
        public static bool IsPreProBuilder4InProject() => false;
        public static bool DoesProjectContainDeprecatedGUIDs() => false;
    }

    static class AssetIdRemapEditor
    {
        public static void OpenConversionEditor() { }
    }
}
#endif
