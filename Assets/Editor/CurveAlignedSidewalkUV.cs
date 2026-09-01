#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace CheatOnYourDayOnes.EditorTools
{
    [InitializeOnLoad]
    public static class CurveAlignedSidewalkUV
    {
        private const string CurveMaterialPath =
            "Assets/Materials/Sidewalk/ConcretePavement02/Concrete Pavement - Curve Aligned.mat";
        private const string WorldMaterialPath =
            "Assets/Materials/Sidewalk/ConcretePavement02/Concrete Pavement - Large Slabs.mat";
        private const string BaseTexturePath =
            "Assets/Materials/Sidewalk/ConcretePavement02/Textures/ConcretePavement02_BaseColor_2K.jpg";
        private const string AutomaticMenuPath =
            "Tools/CYDOY/Sidewalk/Automatic Curve Mapping";
        private const string AutomaticPreference =
            "CYDOY.Sidewalk.AutomaticCurveMapping";
        private const float UvPerMeter = .36f;
        private const float MaximumSegmentLength = .75f;
        private const double ScanInterval = .25d;
        private const double StableEditDelay = .45d;

        private static readonly Dictionary<PolyShape, int> AppliedSignatures =
            new Dictionary<PolyShape, int>();
        private static readonly Dictionary<PolyShape, PendingAlignment> PendingAlignments =
            new Dictionary<PolyShape, PendingAlignment>();
        private static readonly Dictionary<ProBuilderMesh, int> AppliedStraightSignatures =
            new Dictionary<ProBuilderMesh, int>();
        private static readonly Dictionary<ProBuilderMesh, PendingAlignment> PendingStraightAlignments =
            new Dictionary<ProBuilderMesh, PendingAlignment>();
        private static double s_NextScan;

        private struct PendingAlignment
        {
            public int signature;
            public double changedAt;
        }

        static CurveAlignedSidewalkUV()
        {
            EditorApplication.update -= AutomaticUpdate;
            EditorApplication.update += AutomaticUpdate;
            EditorApplication.delayCall += ResetAutomaticState;
        }

        private static bool AutomaticMappingEnabled =>
            EditorPrefs.GetBool(AutomaticPreference, true);

        [MenuItem(AutomaticMenuPath)]
        private static void ToggleAutomaticMapping()
        {
            bool enabled = !AutomaticMappingEnabled;
            EditorPrefs.SetBool(AutomaticPreference, enabled);
            Menu.SetChecked(AutomaticMenuPath, enabled);
            ResetAutomaticState();
            Debug.Log($"[CYDOY SIDEWALK] Automatic curve mapping {(enabled ? "enabled" : "disabled")}.");
        }

        [MenuItem(AutomaticMenuPath, true)]
        private static bool ValidateAutomaticMapping()
        {
            Menu.SetChecked(AutomaticMenuPath, AutomaticMappingEnabled);
            return true;
        }

        private static void ResetAutomaticState()
        {
            AppliedSignatures.Clear();
            PendingAlignments.Clear();
            AppliedStraightSignatures.Clear();
            PendingStraightAlignments.Clear();
            s_NextScan = 0d;
        }

        private static void AutomaticUpdate()
        {
            if (!AutomaticMappingEnabled ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < s_NextScan)
                return;

            double now = EditorApplication.timeSinceStartup;
            s_NextScan = now + ScanInterval;

            Material curveMaterial = AssetDatabase.LoadAssetAtPath<Material>(CurveMaterialPath);
            Material worldMaterial = AssetDatabase.LoadAssetAtPath<Material>(WorldMaterialPath);
            Texture2D baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexturePath);
            if (curveMaterial == null || worldMaterial == null || baseTexture == null) return;

            bool changed = false;
            foreach (PolyShape shape in Object.FindObjectsByType<PolyShape>(FindObjectsInactive.Include))
            {
                if (shape == null || !shape.gameObject.scene.IsValid()) continue;
                MeshRenderer renderer = shape.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                bool usesCurve = UsesMaterial(renderer, curveMaterial);
                bool usesWorld = UsesAutomaticSourceMaterial(
                    renderer,
                    curveMaterial,
                    worldMaterial,
                    baseTexture);
                if (!usesCurve && !usesWorld) continue;

                int signature = CalculateSignature(shape, usesCurve);
                if (usesCurve &&
                    AppliedSignatures.TryGetValue(shape, out int applied) &&
                    applied == signature)
                {
                    PendingAlignments.Remove(shape);
                    continue;
                }

                if (!PendingAlignments.TryGetValue(shape, out PendingAlignment pending) ||
                    pending.signature != signature)
                {
                    PendingAlignments[shape] = new PendingAlignment
                    {
                        signature = signature,
                        changedAt = now
                    };
                    continue;
                }

                if (now - pending.changedAt < StableEditDelay) continue;

                if (usesWorld)
                    AssignCurveMaterial(renderer, curveMaterial, worldMaterial, baseTexture);

                if (!AlignShape(shape)) continue;

                AppliedSignatures[shape] = CalculateSignature(shape, true);
                PendingAlignments.Remove(shape);
                changed = true;
            }

            foreach (ProBuilderMesh mesh in Object.FindObjectsByType<ProBuilderMesh>(FindObjectsInactive.Include))
            {
                if (mesh == null ||
                    !mesh.gameObject.scene.IsValid() ||
                    mesh.GetComponent<PolyShape>() != null)
                    continue;

                MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                bool usesCurve = UsesMaterial(renderer, curveMaterial);
                bool usesWorld = UsesAutomaticSourceMaterial(
                    renderer,
                    curveMaterial,
                    worldMaterial,
                    baseTexture);
                if (!usesCurve && !usesWorld) continue;

                int signature = CalculateStraightSignature(mesh, usesCurve);
                if (usesCurve &&
                    AppliedStraightSignatures.TryGetValue(mesh, out int applied) &&
                    applied == signature)
                {
                    PendingStraightAlignments.Remove(mesh);
                    continue;
                }

                if (!PendingStraightAlignments.TryGetValue(mesh, out PendingAlignment pending) ||
                    pending.signature != signature)
                {
                    PendingStraightAlignments[mesh] = new PendingAlignment
                    {
                        signature = signature,
                        changedAt = now
                    };
                    continue;
                }

                if (now - pending.changedAt < StableEditDelay) continue;

                if (usesWorld)
                    AssignCurveMaterial(renderer, curveMaterial, worldMaterial, baseTexture);

                if (!AlignStraightMesh(mesh)) continue;

                AppliedStraightSignatures[mesh] = CalculateStraightSignature(mesh, true);
                PendingStraightAlignments.Remove(mesh);
                changed = true;
            }

            if (changed) SceneView.RepaintAll();
        }

        private static int CalculateSignature(PolyShape shape, bool usesCurveMaterial)
        {
            unchecked
            {
                int hash = 17;
                IReadOnlyList<Vector3> points = shape.controlPoints;
                hash = hash * 31 + (points?.Count ?? 0);
                if (points != null)
                {
                    foreach (Vector3 point in points)
                    {
                        hash = hash * 31 + point.x.GetHashCode();
                        hash = hash * 31 + point.y.GetHashCode();
                        hash = hash * 31 + point.z.GetHashCode();
                    }
                }
                hash = hash * 31 + shape.extrude.GetHashCode();
                hash = hash * 31 + (usesCurveMaterial ? 1 : 0);
                return hash;
            }
        }

        private static int CalculateStraightSignature(
            ProBuilderMesh mesh,
            bool usesCurveMaterial)
        {
            unchecked
            {
                int hash = 17;
                IList<Vector3> positions = mesh.positions;
                hash = hash * 31 + (positions?.Count ?? 0);
                if (positions != null)
                {
                    foreach (Vector3 point in positions)
                    {
                        hash = hash * 31 + point.x.GetHashCode();
                        hash = hash * 31 + point.y.GetHashCode();
                        hash = hash * 31 + point.z.GetHashCode();
                    }
                }
                hash = hash * 31 + (usesCurveMaterial ? 1 : 0);
                return hash;
            }
        }

        private static bool AlignStraightMesh(ProBuilderMesh mesh)
        {
            IList<Vector3> positions = mesh.positions;
            if (positions == null || positions.Count < 4) return false;

            Vector2 center = Vector2.zero;
            foreach (Vector3 point in positions)
                center += new Vector2(point.x, point.z);
            center /= positions.Count;

            float xx = 0f;
            float xz = 0f;
            float zz = 0f;
            foreach (Vector3 point in positions)
            {
                Vector2 delta = new Vector2(point.x, point.z) - center;
                xx += delta.x * delta.x;
                xz += delta.x * delta.y;
                zz += delta.y * delta.y;
            }

            if (xx + zz < .0001f) return false;
            float angle = .5f * Mathf.Atan2(2f * xz, xx - zz);
            Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 across = new Vector2(-along.y, along.x);

            float minimumAcross = float.MaxValue;
            float maximumAcross = float.MinValue;
            float minimumAlong = float.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 point = new Vector2(positions[i].x, positions[i].z);
                float projectedAcross = Vector2.Dot(point, across);
                float projectedAlong = Vector2.Dot(point, along);
                minimumAcross = Mathf.Min(minimumAcross, projectedAcross);
                maximumAcross = Mathf.Max(maximumAcross, projectedAcross);
                minimumAlong = Mathf.Min(minimumAlong, projectedAlong);
            }

            float acrossCenter = (minimumAcross + maximumAcross) * .5f;
            Vector2[] uv = new Vector2[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 point = new Vector2(positions[i].x, positions[i].z);
                uv[i] = new Vector2(
                    (Vector2.Dot(point, across) - acrossCenter) * UvPerMeter,
                    (Vector2.Dot(point, along) - minimumAlong) * UvPerMeter);
            }

            Undo.RecordObject(mesh, "Align straight sidewalk texture");
            foreach (Face face in mesh.faces)
            {
                IReadOnlyList<int> indexes = face.indexes;
                if (indexes.Count < 3)
                {
                    face.manualUV = false;
                    continue;
                }

                Vector3 a = positions[indexes[0]];
                Vector3 b = positions[indexes[1]];
                Vector3 c = positions[indexes[2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                face.manualUV = Mathf.Abs(normal.y) >= .65f;
            }

            mesh.textures = uv;
            mesh.Refresh(RefreshMask.UV | RefreshMask.Tangents);
            EditorUtility.SetDirty(mesh);
            if (mesh.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(mesh.gameObject.scene);
            return true;
        }

        private static void AssignCurveMaterial(
            MeshRenderer renderer,
            Material curveMaterial,
            Material worldMaterial,
            Texture2D baseTexture)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                materials = new[] { curveMaterial };
            }
            else
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    Material candidate = materials[i];
                    if (candidate == worldMaterial ||
                        IsWorldProjectedSidewalkMaterial(candidate, curveMaterial, baseTexture))
                        materials[i] = curveMaterial;
                }
            }

            Undo.RecordObject(renderer, "Use automatic curve-aligned sidewalk material");
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }

        private static bool UsesAutomaticSourceMaterial(
            Renderer renderer,
            Material curveMaterial,
            Material worldMaterial,
            Texture2D baseTexture)
        {
            foreach (Material candidate in renderer.sharedMaterials)
            {
                if (candidate == worldMaterial ||
                    IsWorldProjectedSidewalkMaterial(candidate, curveMaterial, baseTexture))
                    return true;
            }
            return false;
        }

        private static bool IsWorldProjectedSidewalkMaterial(
            Material material,
            Material curveMaterial,
            Texture2D baseTexture)
        {
            if (material == null || material == curveMaterial) return false;
            if (material.shader != null && material.shader.name == "CYDOY/World Aligned Sidewalk")
                return true;

            Texture texture = null;
            if (material.HasProperty("_BaseMap")) texture = material.GetTexture("_BaseMap");
            if (texture == null && material.HasProperty("_MainTex"))
                texture = material.GetTexture("_MainTex");
            return texture == baseTexture;
        }

        [MenuItem("Tools/CYDOY/Sidewalk/Apply Large Slabs + Align Selected Curves")]
        public static void ApplyAndAlignSelected()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CurveMaterialPath);
            if (material == null)
            {
                Debug.LogError($"[CYDOY SIDEWALK] Material not found at {CurveMaterialPath}");
                return;
            }

            List<PolyShape> shapes = GetSelectedPolyShapes();
            int aligned = 0;
            foreach (PolyShape shape in shapes)
            {
                MeshRenderer renderer = shape.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                Undo.RecordObject(renderer, "Apply curve-aligned sidewalk material");
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    materials = new[] { material };
                else
                    materials[0] = material;
                renderer.sharedMaterials = materials;

                if (AlignShape(shape)) aligned++;
            }

            if (aligned > 0)
            {
                AssetDatabase.SaveAssets();
                SceneView.RepaintAll();
                Debug.Log($"[CYDOY SIDEWALK] Aligned {aligned} selected sidewalk PolyShape(s) along their curves.");
            }
            else
            {
                Debug.LogWarning("[CYDOY SIDEWALK] Select one or more finished sidewalk PolyShapes first.");
            }
        }

        [MenuItem("Tools/CYDOY/Sidewalk/Apply Large Slabs + Align Selected Curves", true)]
        private static bool ValidateApplyAndAlignSelected()
        {
            return GetSelectedPolyShapes().Count > 0;
        }

        [MenuItem("Tools/CYDOY/Sidewalk/Realign All Using Curve Material")]
        public static void RealignAllUsingCurveMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CurveMaterialPath);
            if (material == null) return;

            int aligned = 0;
            foreach (PolyShape shape in Object.FindObjectsByType<PolyShape>(FindObjectsInactive.Include))
            {
                MeshRenderer renderer = shape.GetComponent<MeshRenderer>();
                if (renderer == null || !UsesMaterial(renderer, material)) continue;
                if (AlignShape(shape)) aligned++;
            }

            if (aligned > 0) AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log($"[CYDOY SIDEWALK] Realigned {aligned} curve-material sidewalk PolyShape(s).");
        }

        private static List<PolyShape> GetSelectedPolyShapes()
        {
            List<PolyShape> result = new List<PolyShape>();
            HashSet<PolyShape> unique = new HashSet<PolyShape>();
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected == null) continue;
                foreach (PolyShape shape in selected.GetComponentsInChildren<PolyShape>(true))
                {
                    if (shape != null && unique.Add(shape)) result.Add(shape);
                }
            }
            return result;
        }

        private static bool UsesMaterial(Renderer renderer, Material material)
        {
            foreach (Material candidate in renderer.sharedMaterials)
                if (candidate == material) return true;
            return false;
        }

        private static bool AlignShape(PolyShape shape)
        {
            IReadOnlyList<Vector3> outline = shape.controlPoints;
            ProBuilderMesh mesh = shape.GetComponent<ProBuilderMesh>();
            MeshFilter meshFilter = shape.GetComponent<MeshFilter>();
            if (outline == null || outline.Count < 4 || mesh == null || meshFilter == null ||
                meshFilter.sharedMesh == null || mesh.vertexCount == 0)
                return false;

            if (!TryFindEndCaps(outline, out int firstCap, out int secondCap)) return false;

            List<Vector3> firstSide = BuildForwardChain(outline, (firstCap + 1) % outline.Count, secondCap);
            List<Vector3> secondSide = BuildForwardChain(outline, (secondCap + 1) % outline.Count, firstCap);
            secondSide.Reverse();
            if (firstSide.Count < 2 || secondSide.Count < 2) return false;

            float[] firstDistances = BuildDistances(firstSide);
            float[] secondDistances = BuildDistances(secondSide);
            float pathLength = (firstDistances[^1] + secondDistances[^1]) * .5f;
            if (pathLength < .01f) return false;

            int samplesForLength = Mathf.CeilToInt(pathLength / MaximumSegmentLength) + 1;
            int sampleCount = Mathf.Clamp(
                Mathf.Max(samplesForLength, Mathf.Max(firstSide.Count, secondSide.Count)),
                3,
                512);

            List<Vector3> left = new List<Vector3>(sampleCount);
            List<Vector3> right = new List<Vector3>(sampleCount);
            List<Vector3> center = new List<Vector3>(sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)(sampleCount - 1);
                Vector3 first = SampleChain(firstSide, firstDistances, t);
                Vector3 second = SampleChain(secondSide, secondDistances, t);
                left.Add(first);
                right.Add(second);
                center.Add((first + second) * .5f);
            }

            float[] centerDistances = BuildDistances(center);
            if (centerDistances[^1] < .01f) return false;

            float extrusion = shape.extrude;
            if (Mathf.Abs(extrusion) < .001f) extrusion = .001f;

            List<Vector3> lowerLeft = new List<Vector3>(sampleCount);
            List<Vector3> lowerRight = new List<Vector3>(sampleCount);
            List<Vector3> upperLeft = new List<Vector3>(sampleCount);
            List<Vector3> upperRight = new List<Vector3>(sampleCount);
            Vector3 extrusionOffset = Vector3.up * extrusion;
            for (int i = 0; i < sampleCount; i++)
            {
                if (extrusion >= 0f)
                {
                    lowerLeft.Add(left[i]);
                    lowerRight.Add(right[i]);
                    upperLeft.Add(left[i] + extrusionOffset);
                    upperRight.Add(right[i] + extrusionOffset);
                }
                else
                {
                    lowerLeft.Add(left[i] + extrusionOffset);
                    lowerRight.Add(right[i] + extrusionOffset);
                    upperLeft.Add(left[i]);
                    upperRight.Add(right[i]);
                }
            }

            List<Vector3> rebuiltPositions = new List<Vector3>((sampleCount - 1) * 16 + 8);
            List<Vector2> rebuiltUv = new List<Vector2>((sampleCount - 1) * 16 + 8);
            List<Face> rebuiltFaces = new List<Face>((sampleCount - 1) * 4 + 2);

            for (int i = 0; i < sampleCount - 1; i++)
            {
                float along0 = centerDistances[i] * UvPerMeter;
                float along1 = centerDistances[i + 1] * UvPerMeter;
                float width0 = HorizontalDistance(upperLeft[i], upperRight[i]) * UvPerMeter;
                float width1 = HorizontalDistance(upperLeft[i + 1], upperRight[i + 1]) * UvPerMeter;
                float left0 = width0 * -.5f;
                float right0 = width0 * .5f;
                float left1 = width1 * -.5f;
                float right1 = width1 * .5f;

                AddQuad(
                    rebuiltPositions, rebuiltUv, rebuiltFaces,
                    upperLeft[i], upperRight[i], upperRight[i + 1], upperLeft[i + 1],
                    new Vector2(left0, along0), new Vector2(right0, along0),
                    new Vector2(right1, along1), new Vector2(left1, along1),
                    Vector3.up, 1);

                AddQuad(
                    rebuiltPositions, rebuiltUv, rebuiltFaces,
                    lowerLeft[i], lowerLeft[i + 1], lowerRight[i + 1], lowerRight[i],
                    new Vector2(left0, along0), new Vector2(left1, along1),
                    new Vector2(right1, along1), new Vector2(right0, along0),
                    Vector3.down, 2);

                Vector3 segmentCenter = (center[i] + center[i + 1]) * .5f;
                Vector3 leftOutside = (left[i] + left[i + 1]) * .5f - segmentCenter;
                Vector3 rightOutside = (right[i] + right[i + 1]) * .5f - segmentCenter;
                leftOutside.y = 0f;
                rightOutside.y = 0f;

                AddQuad(
                    rebuiltPositions, rebuiltUv, rebuiltFaces,
                    lowerLeft[i], upperLeft[i], upperLeft[i + 1], lowerLeft[i + 1],
                    new Vector2(0f, 0f), new Vector2(0f, Mathf.Abs(extrusion) * UvPerMeter),
                    new Vector2(along1 - along0, Mathf.Abs(extrusion) * UvPerMeter),
                    new Vector2(along1 - along0, 0f),
                    leftOutside.normalized, 3);

                AddQuad(
                    rebuiltPositions, rebuiltUv, rebuiltFaces,
                    lowerRight[i], lowerRight[i + 1], upperRight[i + 1], upperRight[i],
                    new Vector2(0f, 0f), new Vector2(along1 - along0, 0f),
                    new Vector2(along1 - along0, Mathf.Abs(extrusion) * UvPerMeter),
                    new Vector2(0f, Mathf.Abs(extrusion) * UvPerMeter),
                    rightOutside.normalized, 4);
            }

            Vector3 startOutside = center[0] - center[1];
            startOutside.y = 0f;
            float startWidth = HorizontalDistance(upperLeft[0], upperRight[0]) * UvPerMeter;
            AddQuad(
                rebuiltPositions, rebuiltUv, rebuiltFaces,
                lowerLeft[0], lowerRight[0], upperRight[0], upperLeft[0],
                new Vector2(0f, 0f), new Vector2(startWidth, 0f),
                new Vector2(startWidth, Mathf.Abs(extrusion) * UvPerMeter),
                new Vector2(0f, Mathf.Abs(extrusion) * UvPerMeter),
                startOutside.normalized, 0);

            int last = sampleCount - 1;
            Vector3 endOutside = center[last] - center[last - 1];
            endOutside.y = 0f;
            float endWidth = HorizontalDistance(upperLeft[last], upperRight[last]) * UvPerMeter;
            AddQuad(
                rebuiltPositions, rebuiltUv, rebuiltFaces,
                lowerLeft[last], upperLeft[last], upperRight[last], lowerRight[last],
                new Vector2(0f, 0f), new Vector2(0f, Mathf.Abs(extrusion) * UvPerMeter),
                new Vector2(endWidth, Mathf.Abs(extrusion) * UvPerMeter),
                new Vector2(endWidth, 0f),
                endOutside.normalized, 0);

            Undo.RecordObject(mesh, "Rebuild sidewalk as curve strip");
            Undo.RecordObject(meshFilter.sharedMesh, "Rebuild sidewalk as curve strip");
            mesh.RebuildWithPositionsAndFaces(rebuiltPositions, rebuiltFaces);
            mesh.textures = rebuiltUv;

            // RebuildWithPositionsAndFaces performs an immediate refresh while the new mesh has
            // no UV array yet. ProBuilder consequently changes every face back to Auto UV. Set
            // manual UV after that rebuild so our path-distance coordinates are not overwritten.
            foreach (Face face in mesh.faces)
                face.manualUV = true;

            mesh.Refresh(RefreshMask.All);

            EditorUtility.SetDirty(mesh);
            EditorUtility.SetDirty(meshFilter.sharedMesh);
            EditorUtility.SetDirty(shape);
            if (shape.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(shape.gameObject.scene);
            return true;
        }

        private static void AddQuad(
            List<Vector3> positions,
            List<Vector2> uv,
            List<Face> faces,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector2 uvA,
            Vector2 uvB,
            Vector2 uvC,
            Vector2 uvD,
            Vector3 desiredNormal,
            int smoothingGroup)
        {
            int index = positions.Count;
            positions.Add(a);
            positions.Add(b);
            positions.Add(c);
            positions.Add(d);
            uv.Add(uvA);
            uv.Add(uvB);
            uv.Add(uvC);
            uv.Add(uvD);

            bool forward = Vector3.Dot(Vector3.Cross(b - a, c - a), desiredNormal) >= 0f;
            int[] triangles = forward
                ? new[] { index, index + 1, index + 2, index, index + 2, index + 3 }
                : new[] { index, index + 2, index + 1, index, index + 3, index + 2 };

            Face face = new Face(triangles)
            {
                manualUV = true,
                smoothingGroup = smoothingGroup,
                submeshIndex = 0
            };
            faces.Add(face);
        }

        private static bool TryFindEndCaps(
            IReadOnlyList<Vector3> points,
            out int firstCap,
            out int secondCap)
        {
            firstCap = -1;
            secondCap = -1;
            float bestScore = float.MaxValue;
            int count = points.Count;

            for (int a = 0; a < count; a++)
            for (int b = a + 2; b < count; b++)
            {
                if (a == 0 && b == count - 1) continue;

                float sideA = ChainLength(points, (a + 1) % count, b);
                float sideB = ChainLength(points, (b + 1) % count, a);
                if (sideA < .01f || sideB < .01f) continue;

                float capA = HorizontalDistance(points[a], points[(a + 1) % count]);
                float capB = HorizontalDistance(points[b], points[(b + 1) % count]);
                float sideBalance = Mathf.Abs(sideA - sideB) / Mathf.Max(sideA, sideB);
                float capBalance = Mathf.Abs(capA - capB) / Mathf.Max(.01f, Mathf.Max(capA, capB));
                float capToLengthPenalty = (capA + capB) / Mathf.Max(.01f, sideA + sideB);
                float cornerPenalty =
                    CapCornerPenalty(points, a) +
                    CapCornerPenalty(points, b);

                // A real end cap turns sharply into both long sidewalk edges. Short curve
                // segments can be much smaller than the sidewalk width, so edge length alone
                // must never be used to identify the two ends of the strip.
                float score =
                    cornerPenalty * 4f +
                    sideBalance +
                    capBalance * .25f +
                    capToLengthPenalty * .25f;
                if (score >= bestScore) continue;

                bestScore = score;
                firstCap = a;
                secondCap = b;
            }

            return firstCap >= 0 && secondCap >= 0;
        }

        private static float CapCornerPenalty(IReadOnlyList<Vector3> points, int edge)
        {
            int count = points.Count;
            Vector2 previous = HorizontalDirection(points[(edge - 1 + count) % count], points[edge]);
            Vector2 current = HorizontalDirection(points[edge], points[(edge + 1) % count]);
            Vector2 next = HorizontalDirection(points[(edge + 1) % count], points[(edge + 2) % count]);

            if (previous.sqrMagnitude < .000001f ||
                current.sqrMagnitude < .000001f ||
                next.sqrMagnitude < .000001f)
                return 2f;

            previous.Normalize();
            current.Normalize();
            next.Normalize();
            return Mathf.Abs(Vector2.Dot(previous, current)) +
                   Mathf.Abs(Vector2.Dot(current, next));
        }

        private static Vector2 HorizontalDirection(Vector3 first, Vector3 second)
        {
            return new Vector2(second.x - first.x, second.z - first.z);
        }

        private static float ChainLength(IReadOnlyList<Vector3> points, int start, int end)
        {
            float length = 0f;
            int index = start;
            int guard = 0;
            while (index != end && guard++ <= points.Count)
            {
                int next = (index + 1) % points.Count;
                length += HorizontalDistance(points[index], points[next]);
                index = next;
            }
            return length;
        }

        private static List<Vector3> BuildForwardChain(
            IReadOnlyList<Vector3> points,
            int start,
            int end)
        {
            List<Vector3> result = new List<Vector3>();
            int index = start;
            int guard = 0;
            result.Add(points[index]);
            while (index != end && guard++ <= points.Count)
            {
                index = (index + 1) % points.Count;
                result.Add(points[index]);
            }
            return result;
        }

        private static float[] BuildDistances(IReadOnlyList<Vector3> points)
        {
            float[] distances = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
                distances[i] = distances[i - 1] + HorizontalDistance(points[i - 1], points[i]);
            return distances;
        }

        private static Vector3 SampleChain(
            IReadOnlyList<Vector3> points,
            IReadOnlyList<float> distances,
            float normalizedDistance)
        {
            float target = distances[^1] * Mathf.Clamp01(normalizedDistance);
            int segment = 0;
            while (segment < distances.Count - 2 && distances[segment + 1] < target) segment++;
            float segmentLength = distances[segment + 1] - distances[segment];
            float t = segmentLength > .0001f ? (target - distances[segment]) / segmentLength : 0f;
            return Vector3.Lerp(points[segment], points[segment + 1], t);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
        }
    }
}
#endif
