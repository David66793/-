using System.Collections.Generic;
using Hearthhold.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthhold.UnityClient
{
    // One shared vertex-coloured mesh per model/level, one renderer per instance.
    // Identical source geometry is rasterized by the Windows preview.
    public sealed class ModelViews
    {
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private readonly Dictionary<Texture2D, Material> spriteMaterials = new Dictionary<Texture2D, Material>();
        private Material material;
        public GameObject Building(Building building, Transform parent)
        {
            string key = "building:" + building.Kind + ":" + building.Level;
            GameObject root = Create(key, ModelFactory.Building(building.Kind, building.Level), parent);
            root.name = building.Spec.Name + " #" + building.Id;
            root.transform.position = new Vector3(building.X, 0, building.Z);
            Bounds bounds = root.GetComponent<MeshFilter>().sharedMesh.bounds;
            BoxCollider collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
            root.AddComponent<BuildingHandle>().Id = building.Id;
            AddBuildingSprite(root, building.Kind);
            return root;
        }
        public GameObject Troop(Unit unit, Transform parent)
        {
            GameObject root = Create("troop:" + unit.Kind, ModelFactory.Troop(unit.Kind), parent);
            root.name = unit.Spec.Name; AddTroopSprite(root, unit.Kind); return root;
        }
        public GameObject BuildingPreview(BuildingKind kind, int level, Transform parent)
        {
            GameObject root = Create("building:" + kind + ":" + level, ModelFactory.Building(kind, level), parent);
            root.name = Rules.Spec(kind).Name + " preview"; AddBuildingSprite(root, kind);
            return root;
        }
        public static void Tint(GameObject root, Color color)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(properties);
            }
        }
        private GameObject Create(string key, ModelMesh geometry, Transform parent)
        {
            Mesh mesh;
            if (!meshes.TryGetValue(key, out mesh))
            {
                List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
                List<Color> colors = new List<Color>(); List<int> triangles = new List<int>();
                foreach (ModelFace face in geometry.Faces)
                {
                    int offset = vertices.Count;
                    Color color = new Color32((byte)(face.Color >> 16), (byte)(face.Color >> 8), (byte)face.Color, 255);
                    foreach (ModelPoint point in face.Points)
                    {
                        vertices.Add(new Vector3(point.X, point.Y, point.Z));
                        normals.Add(new Vector3(face.Normal.X, face.Normal.Y, face.Normal.Z)); colors.Add(color.linear);
                    }
                    for (int i = 1; i < face.Points.Length - 1; i++) { triangles.Add(offset); triangles.Add(offset + i); triangles.Add(offset + i + 1); }
                }
                mesh = new Mesh { name = key };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                meshes.Add(key, mesh);
            }
            if (material == null)
            {
                Material template = Resources.Load<Material>("ModelPalette");
                material = template != null ? new Material(template) : new Material(Shader.Find("Hearthhold/VertexLit"));
            }
            GameObject obj = new GameObject(key); obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }
        private void AddBuildingSprite(GameObject root, BuildingKind kind)
        {
            if (kind == BuildingKind.Wall) return;
            string resource; float width;
            switch (kind)
            {
                case BuildingKind.Keep: resource = "GeneratedArt/KeepV061"; width = 5.8f; break;
                case BuildingKind.Mine: resource = "GeneratedArt/MineV061"; width = 4.35f; break;
                case BuildingKind.Reservoir: resource = "GeneratedArt/ReservoirV061"; width = 4.15f; break;
                case BuildingKind.Barracks: resource = "GeneratedArt/BarracksV061"; width = 4.45f; break;
                case BuildingKind.Cannon: resource = "GeneratedArt/CannonV061"; width = 3.75f; break;
                case BuildingKind.Watchtower: resource = "GeneratedArt/WatchtowerV061"; width = 3.25f; break;
                default: return;
            }
            Texture2D image = Resources.Load<Texture2D>(resource);
            if (image == null) return;
            Rect crop = new Rect(0, 0, image.width, image.height);
            float height = width * image.height / image.width;
            GameObject sprite = SpriteObject("Generated " + kind + " art", image, crop, width, height,
                SpriteMaterial(image, "Generated " + kind + " material", false), root.transform);
            float size = Rules.Spec(kind).Size;
            sprite.transform.localPosition = new Vector3(size * 0.5f, 0.03f, size * 0.5f);
            root.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        private void AddTroopSprite(GameObject root, TroopKind kind)
        {
            Texture2D atlas = Resources.Load<Texture2D>("GeneratedArt/TroopAtlasV061");
            if (atlas == null) { Debug.LogError("HEARTHHOLD_TROOP_ART_MISSING: GeneratedArt/TroopAtlasV061"); return; }
            Rect crop; float width;
            switch (kind)
            {
                case TroopKind.Vanguard: crop = new Rect(0, 0, 650, 585); width = 2.1f; break;
                case TroopKind.Ranger: crop = new Rect(650, 0, 682, 600); width = 2.15f; break;
                case TroopKind.Guardian: crop = new Rect(0, 530, 680, 670); width = 2.5f; break;
                default: crop = new Rect(650, 555, 682, 645); width = 2.3f; break;
            }
            float height = width * crop.height / crop.width;
            GameObject sprite = SpriteObject("Generated " + kind + " art", atlas, crop, width, height,
                SpriteMaterial(atlas, "Generated troop material", true), root.transform);
            sprite.transform.localPosition = new Vector3(0, 0.03f, 0);
            sprite.AddComponent<FixedIsometricBillboard>();
            root.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
        private GameObject SpriteObject(string name, Texture2D atlas, Rect crop, float width, float height, Material spriteMaterial, Transform parent)
        {
            string key = "sprite:" + name;
            Mesh mesh;
            if (!meshes.TryGetValue(key, out mesh))
            {
                float u0 = crop.x / atlas.width, u1 = (crop.x + crop.width) / atlas.width;
                float v0 = 1 - (crop.y + crop.height) / atlas.height, v1 = 1 - crop.y / atlas.height;
                mesh = new Mesh { name = key };
                mesh.vertices = new[] { new Vector3(-width * 0.5f, 0, 0), new Vector3(width * 0.5f, 0, 0), new Vector3(-width * 0.5f, height, 0), new Vector3(width * 0.5f, height, 0) };
                mesh.uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u0, v1), new Vector2(u1, v1) };
                mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(key, mesh);
            }
            GameObject sprite = new GameObject(name); sprite.transform.SetParent(parent, false);
            sprite.transform.localRotation = Quaternion.Euler(45, 45, 0);
            sprite.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = sprite.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = spriteMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (name.Contains("Vanguard") || name.Contains("Ranger") || name.Contains("Guardian") || name.Contains("Sapper")) renderer.sortingOrder = 20;
            return sprite;
        }
        private Material SpriteMaterial(Texture2D atlas, string name, bool readableOverlay)
        {
            Material cached;
            if (spriteMaterials.TryGetValue(atlas, out cached)) return cached;
            Material template = Resources.Load<Material>("GeneratedSpritePalette");
            Shader shader = Shader.Find("Hearthhold/ChromaKeySprite");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material result = template != null ? new Material(template) : new Material(shader);
            result.name = name;
            result.SetTexture("_BaseMap", atlas);
            if (result.HasProperty("_KeyColor")) result.SetColor("_KeyColor", Color.magenta);
            if (result.HasProperty("_Threshold")) result.SetFloat("_Threshold", 0.12f);
            if (result.HasProperty("_ZTest")) result.SetFloat("_ZTest", readableOverlay ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            if (result.HasProperty("_ZWrite")) result.SetFloat("_ZWrite", readableOverlay ? 0 : 1);
            if (readableOverlay) result.renderQueue = 3100;
            spriteMaterials.Add(atlas, result);
            return result;
        }
        public void Dispose()
        {
            foreach (Mesh mesh in meshes.Values) Object.Destroy(mesh);
            meshes.Clear(); if (material != null) Object.Destroy(material);
            foreach (Material spriteMaterial in spriteMaterials.Values) Object.Destroy(spriteMaterial);
            spriteMaterials.Clear();
        }
    }
    internal sealed class FixedIsometricBillboard : MonoBehaviour
    {
        private void LateUpdate() { transform.rotation = Quaternion.Euler(45, 45, 0); }
    }
}
