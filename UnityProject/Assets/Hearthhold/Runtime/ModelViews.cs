using System.Collections.Generic;
using Hearthhold.Core;
using UnityEngine;

namespace Hearthhold.UnityClient
{
    // One shared vertex-coloured mesh per model/level, one renderer per instance.
    // Identical source geometry is rasterized by the Windows preview.
    public sealed class ModelViews
    {
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
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
            return root;
        }
        public GameObject Troop(Unit unit, Transform parent)
        {
            GameObject root = Create("troop:" + unit.Kind, ModelFactory.Troop(unit.Kind), parent);
            root.name = unit.Spec.Name; return root;
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
        public void Dispose()
        {
            foreach (Mesh mesh in meshes.Values) Object.Destroy(mesh);
            meshes.Clear(); if (material != null) Object.Destroy(material);
        }
    }
}
