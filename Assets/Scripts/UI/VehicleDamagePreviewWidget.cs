using System.Collections.Generic;
using CheatOnYourDayOnes.Vehicles;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CheatOnYourDayOnes.UI
{
    [RequireComponent(typeof(RawImage))]
    public sealed class VehicleDamagePreviewWidget : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        private static readonly Vector3 PreviewOrigin = new(12000f, -12000f, 12000f);

        private enum PartKind { Body, Engine, Front, Rear, Left, Right, WheelFL, WheelFR, WheelRL, WheelRR }
        private sealed class PreviewPart
        {
            public Material material;
            public Mesh ownedMesh;
            public PartKind kind;
            public bool fixedColor;
        }

        private readonly List<PreviewPart> _parts = new();
        private RawImage _image;
        private RenderTexture _texture;
        private Camera _previewCamera;
        private GameObject _stage;
        private DriveableCar _vehicle;
        private float _nextRender;
        private int _damageRevision=-1;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _image.color = Color.white;
            _image.raycastTarget = false;
            EnsureRenderResources();
        }

        public void SetVehicle(DriveableCar vehicle)
        {
            if (_vehicle == vehicle) return;
            _vehicle = vehicle;
            RebuildPreview();
        }

        private void LateUpdate()
        {
            if (_vehicle == null || _previewCamera == null || Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + .10f;
            if(_damageRevision!=_vehicle.DamageRevision){RebuildPreview();return;}
            UpdateDamageColors();
            _previewCamera.Render();
        }

        private void EnsureRenderResources()
        {
            if (_texture == null)
            {
                _texture = new RenderTexture(384, 512, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CYDOY Vehicle Damage Top View",
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _texture.Create();
                _image.texture = _texture;
            }

            if (_previewCamera != null) return;
            GameObject cameraObject = new("CYDOY Damage Preview Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.layer = PreviewLayer;
            _previewCamera = cameraObject.GetComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.orthographic = true;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCamera.cullingMask = 1 << PreviewLayer;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = true;
            _previewCamera.nearClipPlane = .05f;
            _previewCamera.farClipPlane = 50f;
            _previewCamera.targetTexture = _texture;
        }

        private void RebuildPreview()
        {
            ClearStage();
            if (_vehicle == null) return;
            _damageRevision=_vehicle.DamageRevision;
            EnsureRenderResources();

            _stage = new GameObject("CYDOY Vehicle Damage Preview");
            _stage.hideFlags = HideFlags.HideAndDontSave;
            _stage.layer = PreviewLayer;
            _stage.transform.position = PreviewOrigin;

            Renderer[] sources = _vehicle.GetComponentsInChildren<Renderer>(true);
            Bounds vehicleBounds = CalculateVehicleBounds(sources);
            float frontSign = DetectFrontSign(sources, vehicleBounds.center.z);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;

            foreach (Renderer source in sources)
            {
                if (source == null || source.name=="Body scrape" || !source.enabled || source is ParticleSystemRenderer || source is TrailRenderer || source is LineRenderer) continue;
                Mesh mesh = null;
                Mesh ownedMesh = null;
                if (source is MeshRenderer)
                {
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                else if (source is SkinnedMeshRenderer skinned)
                {
                    ownedMesh = new Mesh { name = source.name + " Damage Preview" };
                    skinned.BakeMesh(ownedMesh);
                    mesh = ownedMesh;
                }
                if (mesh == null) { if (ownedMesh != null) Destroy(ownedMesh); continue; }

                Matrix4x4 relative = _vehicle.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                GameObject clone = new(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.layer = PreviewLayer;
                clone.transform.SetParent(_stage.transform, false);
                ApplyMatrix(clone.transform, relative);
                clone.GetComponent<MeshFilter>().sharedMesh = mesh;

                Vector3 partCenter = relative.MultiplyPoint3x4(mesh.bounds.center);
                PartKind kind = ClassifyPart(source.transform, partCenter, vehicleBounds, frontSign);
                Material material = new(shader) { name = "Damage " + kind, hideFlags = HideFlags.HideAndDontSave };
                SetMaterialColor(material, DamageColor(HealthFor(kind)));
                MeshRenderer renderer = clone.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // The actual wheel geometry sits underneath wide wheel arches in a
                // strict top view. Hide it here and draw four readable wheel modules
                // at the car's real proportions below.
                if (!IsWheel(kind)&&mesh.isReadable)
                {
                    CreateBodyZones(clone,mesh,relative,vehicleBounds,shader,ownedMesh);
                    Destroy(material);
                    continue;
                }
                if (IsWheel(kind))
                {
                    renderer.enabled = false;
                    material.color = Color.clear;
                }
                _parts.Add(new PreviewPart { material = material, ownedMesh = ownedMesh, kind = kind });
            }

            CreateWheelAreas(vehicleBounds, frontSign, shader);
            CreateEngineArea(vehicleBounds, frontSign, shader);

            float aspect = _texture.width / (float)_texture.height;
            float halfLength = Mathf.Max(.4f, vehicleBounds.extents.z);
            // Leave room for the separated wheel modules and their dark borders.
            float halfWidth = Mathf.Max(.25f, vehicleBounds.extents.x * 1.19f);
            _previewCamera.orthographicSize = Mathf.Max(halfLength * 1.055f, halfWidth / aspect * 1.055f);
            _previewCamera.transform.position = PreviewOrigin + new Vector3(vehicleBounds.center.x, vehicleBounds.max.y + 12f, vehicleBounds.center.z);
            _previewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            UpdateDamageColors();
            _previewCamera.Render();
        }

        private void CreateBodyZones(GameObject clone,Mesh source,Matrix4x4 relative,Bounds bounds,Shader shader,Mesh baked)
        {
            var indices=new List<int>[4];
            for(int i=0;i<4;i++)indices[i]=new List<int>();
            Vector3[] vertices=source.vertices;int[] triangles=source.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 p=relative.MultiplyPoint3x4((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3f)-bounds.center;
                float z=p.z/Mathf.Max(.01f,bounds.extents.z);
                int zone=z>.42f?0:z<-.42f?1:p.x<0f?2:3;
                indices[zone].Add(triangles[i]);indices[zone].Add(triangles[i+1]);indices[zone].Add(triangles[i+2]);
            }
            Mesh mesh=Instantiate(source);mesh.name="Damage zones";mesh.subMeshCount=4;
            PartKind[] kinds={PartKind.Front,PartKind.Rear,PartKind.Left,PartKind.Right};
            var materials=new Material[4];
            for(int i=0;i<4;i++)
            {
                mesh.SetTriangles(indices[i],i);
                materials[i]=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                SetMaterialColor(materials[i],DamageColor(HealthFor(kinds[i])));
                _parts.Add(new PreviewPart{material=materials[i],ownedMesh=i==0?mesh:null,kind=kinds[i]});
            }
            clone.GetComponent<MeshFilter>().sharedMesh=mesh;
            clone.GetComponent<MeshRenderer>().sharedMaterials=materials;
            if(baked!=null)Destroy(baked);
        }

        private void CreateEngineArea(Bounds bounds, float frontSign, Shader shader)
        {
            CreateEngineBlock(bounds, frontSign, shader, true);
            CreateEngineBlock(bounds, frontSign, shader, false);
        }

        private void CreateEngineBlock(Bounds bounds, float frontSign, Shader shader, bool outline)
        {
            GameObject engineArea = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engineArea.name = outline ? "Engine Damage Outline" : "Engine Damage Area";
            engineArea.hideFlags = HideFlags.HideAndDontSave;
            engineArea.layer = PreviewLayer;
            engineArea.transform.SetParent(_stage.transform, false);
            float scale = outline ? 1.14f : 1f;
            engineArea.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y + (outline ? .018f : .045f), bounds.center.z + frontSign * bounds.extents.z * .48f);
            engineArea.transform.localScale = new Vector3(bounds.size.x * .48f * scale, .018f, bounds.size.z * .21f * scale);
            Collider collider = engineArea.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Material material = new(shader) { name = outline ? "Damage Engine Outline" : "Damage Engine", hideFlags = HideFlags.HideAndDontSave };
            SetMaterialColor(material, outline ? new Color(.025f,.03f,.035f,1f) : DamageColor(_vehicle.EngineHealth));
            MeshRenderer renderer = engineArea.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _parts.Add(new PreviewPart { material = material, kind = PartKind.Engine, fixedColor = outline });
        }

        private void CreateWheelAreas(Bounds bounds, float frontSign, Shader shader)
        {
            float axleOffset = bounds.extents.z * .66f;
            float lateralOffset = bounds.extents.x * .98f;
            CreateWheelArea(bounds, new Vector3(-lateralOffset, 0f, frontSign * axleOffset), PartKind.WheelFL, shader);
            CreateWheelArea(bounds, new Vector3( lateralOffset, 0f, frontSign * axleOffset), PartKind.WheelFR, shader);
            CreateWheelArea(bounds, new Vector3(-lateralOffset, 0f,-frontSign * axleOffset), PartKind.WheelRL, shader);
            CreateWheelArea(bounds, new Vector3( lateralOffset, 0f,-frontSign * axleOffset), PartKind.WheelRR, shader);
        }

        private void CreateWheelArea(Bounds bounds, Vector3 offset, PartKind kind, Shader shader)
        {
            bool front=kind==PartKind.WheelFL||kind==PartKind.WheelFR;
            bool left=kind==PartKind.WheelFL||kind==PartKind.WheelRL;
            if(_vehicle.TryGetWheelPosition(front,left,out Vector3 position))offset.z=position.z-bounds.center.z;
            CreateWheelBlock(bounds, offset, kind, shader, true);
            CreateWheelBlock(bounds, offset, kind, shader, false);
        }

        private void CreateWheelBlock(Bounds bounds, Vector3 offset, PartKind kind, Shader shader, bool outline)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wheel.name = outline ? kind + " Outline" : kind.ToString();
            wheel.hideFlags = HideFlags.HideAndDontSave;
            wheel.layer = PreviewLayer;
            wheel.transform.SetParent(_stage.transform, false);
            float border = outline ? 1.22f : 1f;
            wheel.transform.localPosition = new Vector3(bounds.center.x + offset.x, bounds.max.y + (outline ? .065f : .09f), bounds.center.z + offset.z);
            wheel.transform.localScale = new Vector3(bounds.size.x * .135f * border, .022f, bounds.size.z * .205f * border);
            Collider collider = wheel.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Material material = new(shader) { name = "Damage " + kind + (outline ? " Outline" : ""), hideFlags = HideFlags.HideAndDontSave };
            SetMaterialColor(material, outline ? new Color(.018f,.022f,.028f,1f) : DamageColor(HealthFor(kind)));
            MeshRenderer renderer = wheel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _parts.Add(new PreviewPart { material = material, kind = kind, fixedColor = outline });
        }

        private void CreateWheelOutline(Transform wheel, Mesh mesh, Shader shader)
        {
            GameObject outline = new(wheel.name + " Outline", typeof(MeshFilter), typeof(MeshRenderer));
            outline.hideFlags = HideFlags.HideAndDontSave;
            outline.layer = PreviewLayer;
            outline.transform.SetParent(_stage.transform, false);
            outline.transform.localPosition = wheel.localPosition + Vector3.down * .018f;
            outline.transform.localRotation = wheel.localRotation;
            outline.transform.localScale = Vector3.Scale(wheel.localScale, new Vector3(1.16f,1f,1.16f));
            outline.GetComponent<MeshFilter>().sharedMesh = mesh;
            Material material = new(shader) { name = "Damage Wheel Outline", hideFlags = HideFlags.HideAndDontSave };
            SetMaterialColor(material,new Color(.018f,.022f,.028f,1f));
            MeshRenderer renderer = outline.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _parts.Add(new PreviewPart { material = material, kind = PartKind.Body, fixedColor = true });
        }

        private Bounds CalculateVehicleBounds(Renderer[] renderers)
        {
            Bounds result = new(Vector3.zero, Vector3.zero);
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.name=="Body scrape" || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 matrix = _vehicle.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Bounds meshBounds = mesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                    else result.Encapsulate(point);
                }
            }
            return initialized ? result : new Bounds(Vector3.zero, new Vector3(2f, 1f, 4f));
        }

        private float DetectFrontSign(Renderer[] renderers, float fallbackCenter)
        {
            float frontZ = 0f, rearZ = 0f;
            int frontCount = 0, rearCount = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                string path = HierarchyPath(renderer.transform).ToLowerInvariant();
                float z = _vehicle.transform.InverseTransformPoint(renderer.bounds.center).z;
                if (path.Contains("front")) { frontZ += z; frontCount++; }
                if (path.Contains("rear")) { rearZ += z; rearCount++; }
            }
            if (frontCount > 0 && rearCount > 0) return frontZ / frontCount >= rearZ / rearCount ? 1f : -1f;
            return fallbackCenter >= 0f ? 1f : -1f;
        }

        private PartKind ClassifyPart(Transform source, Vector3 center, Bounds bounds, float frontSign)
        {
            string path = HierarchyPath(source).ToLowerInvariant();
            bool wheel = path.Contains("wheel") || path.Contains("tire") || path.Contains("tyre") || path.Contains("reifen") || path.Contains("felge") || path.Contains("rim");
            bool front = path.Contains("front") || (center.z - bounds.center.z) * frontSign > 0f;
            bool left = path.Contains("left") || (!path.Contains("right") && center.x < bounds.center.x);
            if (wheel) return front ? (left ? PartKind.WheelFL : PartKind.WheelFR) : (left ? PartKind.WheelRL : PartKind.WheelRR);

            float longitudinal = (center.z - bounds.center.z) * frontSign / Mathf.Max(.01f, bounds.extents.z);
            float lateral = (center.x - bounds.center.x) / Mathf.Max(.01f, bounds.extents.x);
            if (longitudinal > .58f) return PartKind.Front;
            if (longitudinal > .22f && Mathf.Abs(lateral) < .58f) return PartKind.Engine;
            if (longitudinal < -.55f) return PartKind.Rear;
            if (lateral < -.26f) return PartKind.Left;
            if (lateral > .26f) return PartKind.Right;
            return PartKind.Body;
        }

        private string HierarchyPath(Transform source)
        {
            string result = source.name;
            Transform current = source.parent;
            while (current != null && current != _vehicle.transform) { result += "/" + current.name; current = current.parent; }
            return result;
        }

        private void UpdateDamageColors()
        {
            foreach (PreviewPart part in _parts) if (part.material != null&&!part.fixedColor) SetMaterialColor(part.material, DamageColor(HealthFor(part.kind)));
        }

        private static bool IsWheel(PartKind kind)=>kind==PartKind.WheelFL||kind==PartKind.WheelFR||kind==PartKind.WheelRL||kind==PartKind.WheelRR;

        private float HealthFor(PartKind kind) => kind switch
        {
            PartKind.Engine => _vehicle.EngineHealth,
            PartKind.Front => _vehicle.FrontBodyHealth,
            PartKind.Rear => _vehicle.RearBodyHealth,
            PartKind.Left => _vehicle.LeftBodyHealth,
            PartKind.Right => _vehicle.RightBodyHealth,
            PartKind.WheelFL => _vehicle.FrontLeftWheelHealth,
            PartKind.WheelFR => _vehicle.FrontRightWheelHealth,
            PartKind.WheelRL => _vehicle.RearLeftWheelHealth,
            PartKind.WheelRR => _vehicle.RearRightWheelHealth,
            _ => _vehicle.BodyHealth
        };

        private static Color DamageColor(float health)
        {
            float value = Mathf.Clamp01(health / 100f);
            Color red = new(.95f, .08f, .06f, 1f);
            Color amber = new(1f, .58f, .06f, 1f);
            Color green = new(.16f, .92f, .38f, 1f);
            return value > .5f ? Color.Lerp(amber, green, Mathf.InverseLerp(.5f, 1f, value)) : Color.Lerp(red, amber, Mathf.InverseLerp(0f, .5f, value));
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void ApplyMatrix(Transform target, Matrix4x4 matrix)
        {
            Vector3 right = matrix.GetColumn(0), up = matrix.GetColumn(1), forward = matrix.GetColumn(2);
            Vector3 scale = new(right.magnitude, up.magnitude, forward.magnitude);
            if (Vector3.Dot(Vector3.Cross(right, up), forward) < 0f) scale.x = -scale.x;
            target.localPosition = matrix.GetColumn(3);
            target.localRotation = Quaternion.LookRotation(forward / Mathf.Max(.0001f, scale.z), up / Mathf.Max(.0001f, scale.y));
            target.localScale = scale;
        }

        private void ClearStage()
        {
            foreach (PreviewPart part in _parts)
            {
                if (part.material != null) Destroy(part.material);
                if (part.ownedMesh != null) Destroy(part.ownedMesh);
            }
            _parts.Clear();
            if (_stage != null) Destroy(_stage);
            _stage = null;
        }

        private void OnDestroy()
        {
            ClearStage();
            if (_previewCamera != null) Destroy(_previewCamera.gameObject);
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
        }
    }
}
