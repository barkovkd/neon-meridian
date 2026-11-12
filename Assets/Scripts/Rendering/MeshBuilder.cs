using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonMeridian
{
    /// <summary>
    /// Accumulates quads, boxes and cylinders into one mesh. The whole static city is baked through this so a
    /// city block costs a handful of draw calls instead of thousands of objects.
    /// Winding is clockwise (Unity front faces); every face also gets a tangent so normal maps work.
    /// </summary>
    public sealed class MeshBuilder
    {
        public const int FaceFront = 1, FaceBack = 2, FaceRight = 4, FaceLeft = 8, FaceTop = 16, FaceBottom = 32;
        public const int Sides = FaceFront | FaceBack | FaceRight | FaceLeft;
        public const int AllFaces = Sides | FaceTop | FaceBottom;
        public const int NoBottom = Sides | FaceTop;

        readonly List<Vector3> vertices = new List<Vector3>(2048);
        readonly List<Vector3> normals = new List<Vector3>(2048);
        readonly List<Vector4> tangents = new List<Vector4>(2048);
        readonly List<Vector2> uvs = new List<Vector2>(2048);
        readonly List<Color32> colors = new List<Color32>(2048);
        readonly List<int> triangles = new List<int>(4096);

        /// <summary>Vertex colour given to everything added from now on.</summary>
        public Color32 color = new Color32(255, 255, 255, 255);

        /// <summary>
        /// When set, every vertex added gets this UV instead of the computed one. Used to paint geometry with a
        /// single swatch of an atlas (neon tubes sampling one palette texel).
        /// </summary>
        public Vector2? fixedUv;

        Vector2 Uv(float u, float v) => fixedUv ?? new Vector2(u, v);

        public int VertexCount => vertices.Count;
        public bool IsEmpty => triangles.Count == 0;

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            tangents.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            color = new Color32(255, 255, 255, 255);
            fixedUv = null;
        }

        /// <summary>
        /// Quad defined by its bottom-left corner and two edge vectors as seen by a viewer the face points at:
        /// <paramref name="right"/> goes to the viewer's right, <paramref name="up"/> to the viewer's up.
        /// </summary>
        public void Face(Vector3 bl, Vector3 right, Vector3 up, float u0 = 0f, float v0 = 0f, float u1 = 1f, float v1 = 1f)
        {
            FaceColored(bl, right, up, u0, v0, u1, v1, color, color);
        }

        /// <summary>Same as <see cref="Face"/> but with different vertex colours on the bottom and top edges (used for light shafts).</summary>
        public void FaceColored(Vector3 bl, Vector3 right, Vector3 up, float u0, float v0, float u1, float v1, Color32 bottom, Color32 top)
        {
            int i = vertices.Count;
            Vector3 n = Vector3.Cross(up, right).normalized;
            Vector3 tn = right.normalized;
            Vector4 t = new Vector4(tn.x, tn.y, tn.z, -1f);

            vertices.Add(bl);
            vertices.Add(bl + up);
            vertices.Add(bl + up + right);
            vertices.Add(bl + right);
            for (int k = 0; k < 4; k++)
            {
                normals.Add(n);
                tangents.Add(t);
            }

            uvs.Add(Uv(u0, v0));
            uvs.Add(Uv(u0, v1));
            uvs.Add(Uv(u1, v1));
            uvs.Add(Uv(u1, v0));
            colors.Add(bottom);
            colors.Add(top);
            colors.Add(top);
            colors.Add(bottom);

            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }

        /// <summary>
        /// Free-form quad from four corners given clockwise as seen from the front: bottom-left, top-left, top-right,
        /// bottom-right. Bottom and top edges can carry different vertex colours (light shafts fade this way).
        /// </summary>
        public void Quad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Color32 bottom, Color32 top,
            float u0 = 0f, float v0 = 0f, float u1 = 1f, float v1 = 1f)
        {
            int i = vertices.Count;
            Vector3 n = Vector3.Cross(tl - bl, br - bl).normalized;
            Vector3 tn = (br - bl).normalized;
            Vector4 t = new Vector4(tn.x, tn.y, tn.z, -1f);

            vertices.Add(bl);
            vertices.Add(tl);
            vertices.Add(tr);
            vertices.Add(br);
            for (int k = 0; k < 4; k++)
            {
                normals.Add(n);
                tangents.Add(t);
            }

            uvs.Add(Uv(u0, v0));
            uvs.Add(Uv(u0, v1));
            uvs.Add(Uv(u1, v1));
            uvs.Add(Uv(u1, v0));
            colors.Add(bottom);
            colors.Add(top);
            colors.Add(top);
            colors.Add(bottom);

            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }

        /// <summary>Quad visible from both sides (two faces back to back, text is not mirrored on the back).</summary>
        public void DoubleFace(Vector3 bl, Vector3 right, Vector3 up, float u0 = 0f, float v0 = 0f, float u1 = 1f, float v1 = 1f)
        {
            Face(bl, right, up, u0, v0, u1, v1);
            Face(bl + right, -right, up, u0, v0, u1, v1);
        }

        /// <summary>Axis-aligned box. UVs are in metres divided by <paramref name="uvScale"/> (0 = stretch 0..1 over each face).</summary>
        public void Box(Vector3 center, Vector3 size, int faces = NoBottom, float uvScale = 0f)
        {
            Box(center, size, Quaternion.identity, faces, uvScale);
        }

        public void Box(Vector3 center, Vector3 size, Quaternion rotation, int faces = AllFaces, float uvScale = 0f)
        {
            Vector3 ax = rotation * new Vector3(size.x, 0f, 0f);
            Vector3 ay = rotation * new Vector3(0f, size.y, 0f);
            Vector3 az = rotation * new Vector3(0f, 0f, size.z);
            Vector3 hx = ax * 0.5f, hy = ay * 0.5f, hz = az * 0.5f;

            float ux = uvScale > 0f ? size.x / uvScale : 1f;
            float uy = uvScale > 0f ? size.y / uvScale : 1f;
            float uz = uvScale > 0f ? size.z / uvScale : 1f;

            if ((faces & FaceFront) != 0) Face(center - hx - hy - hz, ax, ay, 0f, 0f, ux, uy);
            if ((faces & FaceBack) != 0) Face(center + hx - hy + hz, -ax, ay, 0f, 0f, ux, uy);
            if ((faces & FaceRight) != 0) Face(center + hx - hy - hz, az, ay, 0f, 0f, uz, uy);
            if ((faces & FaceLeft) != 0) Face(center - hx - hy + hz, -az, ay, 0f, 0f, uz, uy);
            if ((faces & FaceTop) != 0) Face(center - hx + hy - hz, ax, az, 0f, 0f, ux, uz);
            if ((faces & FaceBottom) != 0) Face(center - hx - hy + hz, ax, -az, 0f, 0f, ux, uz);
        }

        /// <summary>Box given by the centre of its base instead of its centre.</summary>
        public void BoxOnGround(Vector3 baseCenter, Vector3 size, int faces = NoBottom, float uvScale = 0f)
        {
            Box(baseCenter + new Vector3(0f, size.y * 0.5f, 0f), size, Quaternion.identity, faces, uvScale);
        }

        /// <summary>
        /// The four walls of an axis-aligned building volume, with window-grid UVs: every wall starts and ends on a
        /// window cell boundary and gets a random offset into the facade texture so neighbours never match.
        /// </summary>
        public void FacadeWalls(Vector3 baseCenter, float sizeX, float height, float sizeZ, float cellW, float cellH,
            int texCellsX, int texCellsY, Rng rng, int faces = Sides)
        {
            float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
            Vector3 up = new Vector3(0f, height, 0f);
            int floors = Mathf.Max(1, Mathf.RoundToInt(height / cellH));

            for (int side = 0; side < 4; side++)
            {
                int bit = 1 << side;
                if ((faces & bit) == 0) continue;

                Vector3 bl, right;
                float width;
                switch (side)
                {
                    case 0: bl = baseCenter + new Vector3(-hx, 0f, -hz); right = new Vector3(sizeX, 0f, 0f); width = sizeX; break;   // front (-Z)
                    case 1: bl = baseCenter + new Vector3(hx, 0f, hz); right = new Vector3(-sizeX, 0f, 0f); width = sizeX; break;    // back (+Z)
                    case 2: bl = baseCenter + new Vector3(hx, 0f, -hz); right = new Vector3(0f, 0f, sizeZ); width = sizeZ; break;    // right (+X)
                    default: bl = baseCenter + new Vector3(-hx, 0f, hz); right = new Vector3(0f, 0f, -sizeZ); width = sizeZ; break;  // left (-X)
                }

                int cells = Mathf.Max(1, Mathf.RoundToInt(width / cellW));
                int offX = rng.Range(0, texCellsX);
                int offY = rng.Range(0, texCellsY);
                float u0 = offX / (float)texCellsX;
                float v0 = offY / (float)texCellsY;
                Face(bl, right, up, u0, v0, u0 + cells / (float)texCellsX, v0 + floors / (float)texCellsY);
            }
        }

        /// <summary>Cylinder or cone frustum from <paramref name="baseCenter"/> along <paramref name="axis"/> (its length is the height).</summary>
        public void Cylinder(Vector3 baseCenter, Vector3 axis, float radiusBottom, float radiusTop, int segments = 10, bool capTop = true, bool capBottom = false)
        {
            float height = axis.magnitude;
            if (height < 1e-5f) return;
            Vector3 n = axis / height;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            // (u, n, v) has the same handedness as (X, Y, Z), which is what the winding below assumes
            Vector3 v = Vector3.Cross(u, n);

            int start = vertices.Count;
            float slope = (radiusBottom - radiusTop) / height;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Vector3 normal = (radial + n * slope).normalized;
                Vector3 tangentDir = Vector3.Cross(radial, n).normalized;
                Vector4 tangent = new Vector4(tangentDir.x, tangentDir.y, tangentDir.z, -1f);

                vertices.Add(baseCenter + radial * radiusBottom);
                vertices.Add(baseCenter + axis + radial * radiusTop);
                normals.Add(normal);
                normals.Add(normal);
                tangents.Add(tangent);
                tangents.Add(tangent);
                uvs.Add(Uv(i / (float)segments, 0f));
                uvs.Add(Uv(i / (float)segments, 1f));
                colors.Add(color);
                colors.Add(color);
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = start + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                triangles.Add(b0);
                triangles.Add(t0);
                triangles.Add(t1);
                triangles.Add(b0);
                triangles.Add(t1);
                triangles.Add(b1);
            }

            if (capTop && radiusTop > 1e-4f) Cap(baseCenter + axis, n, u, v, radiusTop, segments, true);
            if (capBottom && radiusBottom > 1e-4f) Cap(baseCenter, -n, u, v, radiusBottom, segments, false);
        }

        void Cap(Vector3 center, Vector3 normal, Vector3 u, Vector3 v, float radius, int segments, bool top)
        {
            int c = vertices.Count;
            Vector4 tangent = new Vector4(u.x, u.y, u.z, -1f);
            vertices.Add(center);
            normals.Add(normal);
            tangents.Add(tangent);
            uvs.Add(Uv(0.5f, 0.5f));
            colors.Add(color);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                vertices.Add(center + (u * cs + v * sn) * radius);
                normals.Add(normal);
                tangents.Add(tangent);
                uvs.Add(Uv(0.5f + cs * 0.5f, 0.5f + sn * 0.5f));
                colors.Add(color);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(c);
                if (top)
                {
                    triangles.Add(c + 2 + i);
                    triangles.Add(c + 1 + i);
                }
                else
                {
                    triangles.Add(c + 1 + i);
                    triangles.Add(c + 2 + i);
                }
            }
        }

        /// <summary>Thin square beam between two points - cables, rails, braces, neon tubes.</summary>
        public void Beam(Vector3 from, Vector3 to, float thickness, float thicknessY = -1f)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Quaternion rot = Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.99f ? Vector3.forward : Vector3.up);
            Box((from + to) * 0.5f, new Vector3(thickness, thicknessY > 0f ? thicknessY : thickness, len), rot, AllFaces);
        }

        /// <summary>A sagging cable made of a few beam segments.</summary>
        public void Cable(Vector3 from, Vector3 to, float sag, float thickness, int segments = 6)
        {
            Vector3 prev = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                Beam(prev, p, thickness);
                prev = p;
            }
        }

        public Mesh ToMesh(string name, bool keepReadable = false)
        {
            Mesh mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            if (!keepReadable) mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Creates a static GameObject showing the accumulated mesh, or returns null when nothing was added.</summary>
        public GameObject ToObject(string name, Transform parent, Material material, int layer = Layers.World, bool castShadows = false)
        {
            if (IsEmpty) return null;
            GameObject go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = castShadows;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            return go;
        }
    }

    /// <summary>Unity's built-in primitive meshes plus helpers to assemble props, enemies and weapons out of them.</summary>
    public static class Prims
    {
        static Mesh cube, sphere, cylinder, capsule, quad;

        public static Mesh Cube => cube != null ? cube : (cube = Fetch(PrimitiveType.Cube));
        public static Mesh Sphere => sphere != null ? sphere : (sphere = Fetch(PrimitiveType.Sphere));
        public static Mesh Cylinder => cylinder != null ? cylinder : (cylinder = Fetch(PrimitiveType.Cylinder));
        public static Mesh Capsule => capsule != null ? capsule : (capsule = Fetch(PrimitiveType.Capsule));
        public static Mesh Quad => quad != null ? quad : (quad = Fetch(PrimitiveType.Quad));

        static Mesh Fetch(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }

        /// <summary>A renderer-only child object (no collider).</summary>
        public static GameObject Make(Mesh mesh, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, string name = "Part")
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 localPosition, Vector3 size, Material material, string name = "Box")
        {
            return Make(Cube, parent, localPosition, size, material, name);
        }

        public static GameObject Ball(Transform parent, Vector3 localPosition, float diameter, Material material, string name = "Ball")
        {
            return Make(Sphere, parent, localPosition, Vector3.one * diameter, material, name);
        }

        /// <summary>Cylinder along local Y with the given diameter and full height.</summary>
        public static GameObject Rod(Transform parent, Vector3 localPosition, float diameter, float height, Material material, string name = "Rod")
        {
            return Make(Cylinder, parent, localPosition, new Vector3(diameter, height * 0.5f, diameter), material, name);
        }

        public static GameObject Pill(Transform parent, Vector3 localPosition, float diameter, float height, Material material, string name = "Pill")
        {
            return Make(Capsule, parent, localPosition, new Vector3(diameter, height * 0.5f, diameter), material, name);
        }

        public static GameObject Empty(Transform parent, Vector3 localPosition, string name)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }
}
