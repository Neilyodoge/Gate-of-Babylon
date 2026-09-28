using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectR.Effects.Editor
{
    internal static class MeshSdfBaker
    {
        internal readonly struct BakeResult
        {
            public readonly Texture3D Texture;
            public readonly Bounds PaddedBounds;

            public BakeResult(Texture3D texture, Bounds paddedBounds)
            {
                Texture = texture;
                PaddedBounds = paddedBounds;
            }
        }

        private readonly struct Triangle
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 C;
            public readonly Vector3 Min;
            public readonly Vector3 Max;
            public readonly Vector3 Center;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                B = b;
                C = c;
                Min = Vector3.Min(a, Vector3.Min(b, c));
                Max = Vector3.Max(a, Vector3.Max(b, c));
                Center = (a + b + c) / 3f;
            }
        }

        private sealed class Node
        {
            public Vector3 Min;
            public Vector3 Max;
            public int Start;
            public int Count;
            public Node Left;
            public Node Right;

            public bool IsLeaf => Left == null;
        }

        public static BakeResult BakeToAsset(
            Mesh source,
            int resolution,
            string outputPath,
            float minimumPaddingFraction = 0f)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ReadMeshData(source, out Vector3[] sourceVertices, out int[] sourceIndices);
            if (sourceVertices.Length == 0 || sourceIndices.Length < 3)
            {
                throw new InvalidOperationException($"网格“{source.name}”没有可供烘焙的三角形。");
            }

            resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 16, 128));
            Bounds paddedBounds = CalculatePaddedBounds(
                source.bounds,
                resolution,
                minimumPaddingFraction);
            Vector3 boundsMin = paddedBounds.min;
            Vector3 boundsSize = paddedBounds.size;
            var normalizedVertices = new Vector3[sourceVertices.Length];
            for (int index = 0; index < sourceVertices.Length; index++)
            {
                Vector3 vertex = sourceVertices[index] - boundsMin;
                normalizedVertices[index] = new Vector3(
                    vertex.x / boundsSize.x,
                    vertex.y / boundsSize.y,
                    vertex.z / boundsSize.z);
            }

            int triangleCount = sourceIndices.Length / 3;
            var triangles = new Triangle[triangleCount];
            var triangleOrder = new int[triangleCount];
            for (int index = 0; index < triangleCount; index++)
            {
                int first = index * 3;
                triangles[index] = new Triangle(
                    normalizedVertices[sourceIndices[first]],
                    normalizedVertices[sourceIndices[first + 1]],
                    normalizedVertices[sourceIndices[first + 2]]);
                triangleOrder[index] = index;
            }

            Node root = BuildNode(triangles, triangleOrder, 0, triangleOrder.Length);
            var pixels = new Color[resolution * resolution * resolution];

            Parallel.For(0, resolution, z =>
            {
                var lineIntersections = new List<float>(64);
                for (int y = 0; y < resolution; y++)
                {
                    float sampleY = (y + 0.5f) / resolution;
                    float sampleZ = (z + 0.5f) / resolution;
                    lineIntersections.Clear();
                    CollectLineIntersections(
                        root,
                        triangles,
                        triangleOrder,
                        new Vector3(-1f, sampleY + 1.37e-6f, sampleZ + 2.11e-6f),
                        lineIntersections);
                    lineIntersections.Sort();
                    RemoveNearDuplicates(lineIntersections);
                    int passedIntersections = 0;

                    for (int x = 0; x < resolution; x++)
                    {
                        Vector3 point = new Vector3(
                            (x + 0.5f) / resolution,
                            sampleY,
                            sampleZ);
                        while (passedIntersections < lineIntersections.Count &&
                               lineIntersections[passedIntersections] < point.x)
                        {
                            passedIntersections++;
                        }

                        float nearestSquared = float.PositiveInfinity;
                        FindNearestSquared(root, triangles, triangleOrder, point, ref nearestSquared);
                        float signedDistance = Mathf.Sqrt(nearestSquared);
                        if ((passedIntersections & 1) == 0)
                        {
                            signedDistance = -signedDistance;
                        }

                        pixels[x + resolution * (y + resolution * z)] =
                            new Color(signedDistance, 0f, 0f, 1f);
                    }
                }
            });

            var generated = new Texture3D(
                resolution, resolution, resolution, TextureFormat.RHalf, false)
            {
                name = $"{source.name}_SDF_{resolution}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0
            };
            generated.SetPixels(pixels);
            generated.Apply(false, false);

            string folder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                CreateFolders(folder);
            }

            Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(outputPath);
            Texture3D result;
            if (existing != null)
            {
                EditorUtility.CopySerialized(generated, existing);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                result = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(generated, outputPath);
                result = generated;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            return new BakeResult(result, paddedBounds);
        }

        private static void ReadMeshData(
            Mesh source,
            out Vector3[] vertices,
            out int[] indices)
        {
            using (Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData meshData = meshDataArray[0];
                using (var nativeVertices =
                       new NativeArray<Vector3>(meshData.vertexCount, Allocator.Temp))
                {
                    meshData.GetVertices(nativeVertices);
                    vertices = nativeVertices.ToArray();
                }

                int indexCount = 0;
                for (int subMesh = 0; subMesh < meshData.subMeshCount; subMesh++)
                {
                    indexCount += meshData.GetSubMesh(subMesh).indexCount;
                }

                indices = new int[indexCount];
                int destination = 0;
                for (int subMesh = 0; subMesh < meshData.subMeshCount; subMesh++)
                {
                    SubMeshDescriptor descriptor = meshData.GetSubMesh(subMesh);
                    if (meshData.indexFormat == IndexFormat.UInt16)
                    {
                        using var subMeshIndices =
                            new NativeArray<ushort>(descriptor.indexCount, Allocator.Temp);
                        meshData.GetIndices(subMeshIndices, subMesh);
                        for (int index = 0; index < subMeshIndices.Length; index++)
                        {
                            indices[destination++] = subMeshIndices[index];
                        }
                    }
                    else
                    {
                        using var subMeshIndices =
                            new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                        meshData.GetIndices(subMeshIndices, subMesh);
                        for (int index = 0; index < subMeshIndices.Length; index++)
                        {
                            indices[destination++] = subMeshIndices[index];
                        }
                    }
                }
            }
        }

        private static Bounds CalculatePaddedBounds(
            Bounds bounds,
            int resolution,
            float minimumPaddingFraction)
        {
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, 0.0001f);
            size.y = Mathf.Max(size.y, 0.0001f);
            size.z = Mathf.Max(size.z, 0.0001f);
            float paddingFraction = Mathf.Max(
                2f / Mathf.Max(resolution - 4, 1),
                Mathf.Max(minimumPaddingFraction, 0f));
            Vector3 padding = size * paddingFraction;
            return new Bounds(bounds.center, size + padding * 2f);
        }

        private static Node BuildNode(Triangle[] triangles, int[] order, int start, int count)
        {
            var node = new Node
            {
                Start = start,
                Count = count,
                Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity)
            };
            Vector3 centroidMin = node.Min;
            Vector3 centroidMax = node.Max;
            for (int index = start; index < start + count; index++)
            {
                Triangle triangle = triangles[order[index]];
                node.Min = Vector3.Min(node.Min, triangle.Min);
                node.Max = Vector3.Max(node.Max, triangle.Max);
                centroidMin = Vector3.Min(centroidMin, triangle.Center);
                centroidMax = Vector3.Max(centroidMax, triangle.Center);
            }

            if (count <= 8)
            {
                return node;
            }

            Vector3 centroidSize = centroidMax - centroidMin;
            int axis = centroidSize.x >= centroidSize.y && centroidSize.x >= centroidSize.z
                ? 0
                : centroidSize.y >= centroidSize.z ? 1 : 2;
            Array.Sort(
                order,
                start,
                count,
                Comparer<int>.Create((left, right) =>
                    GetAxis(triangles[left].Center, axis).CompareTo(
                        GetAxis(triangles[right].Center, axis))));

            int leftCount = count / 2;
            node.Left = BuildNode(triangles, order, start, leftCount);
            node.Right = BuildNode(triangles, order, start + leftCount, count - leftCount);
            return node;
        }

        private static void FindNearestSquared(
            Node node,
            Triangle[] triangles,
            int[] order,
            Vector3 point,
            ref float nearestSquared)
        {
            if (SquaredDistanceToBounds(point, node.Min, node.Max) >= nearestSquared)
            {
                return;
            }

            if (node.IsLeaf)
            {
                for (int index = node.Start; index < node.Start + node.Count; index++)
                {
                    Triangle triangle = triangles[order[index]];
                    nearestSquared = Mathf.Min(
                        nearestSquared,
                        SquaredDistanceToTriangle(point, triangle.A, triangle.B, triangle.C));
                }
                return;
            }

            float leftDistance = SquaredDistanceToBounds(point, node.Left.Min, node.Left.Max);
            float rightDistance = SquaredDistanceToBounds(point, node.Right.Min, node.Right.Max);
            if (leftDistance <= rightDistance)
            {
                FindNearestSquared(node.Left, triangles, order, point, ref nearestSquared);
                FindNearestSquared(node.Right, triangles, order, point, ref nearestSquared);
            }
            else
            {
                FindNearestSquared(node.Right, triangles, order, point, ref nearestSquared);
                FindNearestSquared(node.Left, triangles, order, point, ref nearestSquared);
            }
        }

        private static void CollectLineIntersections(
            Node node,
            Triangle[] triangles,
            int[] order,
            Vector3 origin,
            List<float> intersections)
        {
            if (!RayIntersectsBounds(origin, Vector3.right, node.Min, node.Max))
            {
                return;
            }

            if (node.IsLeaf)
            {
                for (int index = node.Start; index < node.Start + node.Count; index++)
                {
                    Triangle triangle = triangles[order[index]];
                    if (RayIntersectsTriangle(
                            origin,
                            Vector3.right,
                            triangle.A,
                            triangle.B,
                            triangle.C,
                            out float distance))
                    {
                        intersections.Add(origin.x + distance);
                    }
                }
                return;
            }

            CollectLineIntersections(node.Left, triangles, order, origin, intersections);
            CollectLineIntersections(node.Right, triangles, order, origin, intersections);
        }

        private static void RemoveNearDuplicates(List<float> intersections)
        {
            for (int index = intersections.Count - 1; index > 0; index--)
            {
                if (Mathf.Abs(intersections[index] - intersections[index - 1]) < 1e-5f)
                {
                    intersections.RemoveAt(index);
                }
            }
        }

        private static float SquaredDistanceToBounds(Vector3 point, Vector3 min, Vector3 max)
        {
            float dx = Mathf.Max(min.x - point.x, 0f, point.x - max.x);
            float dy = Mathf.Max(min.y - point.y, 0f, point.y - max.y);
            float dz = Mathf.Max(min.z - point.z, 0f, point.z - max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        private static float SquaredDistanceToTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
            {
                return ap.sqrMagnitude;
            }

            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
            {
                return bp.sqrMagnitude;
            }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return (point - (a + v * ab)).sqrMagnitude;
            }

            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
            {
                return cp.sqrMagnitude;
            }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return (point - (a + w * ac)).sqrMagnitude;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return (point - (b + w * (c - b))).sqrMagnitude;
            }

            float denominator = 1f / (va + vb + vc);
            float faceV = vb * denominator;
            float faceW = vc * denominator;
            return (point - (a + ab * faceV + ac * faceW)).sqrMagnitude;
        }

        private static bool RayIntersectsBounds(
            Vector3 origin,
            Vector3 direction,
            Vector3 min,
            Vector3 max)
        {
            float near = 0f;
            float far = float.PositiveInfinity;
            for (int axis = 0; axis < 3; axis++)
            {
                float component = GetAxis(direction, axis);
                float axisOrigin = GetAxis(origin, axis);
                float axisMin = GetAxis(min, axis);
                float axisMax = GetAxis(max, axis);
                if (Mathf.Abs(component) < 1e-8f)
                {
                    if (axisOrigin < axisMin || axisOrigin > axisMax)
                    {
                        return false;
                    }
                    continue;
                }

                float inverse = 1f / component;
                float first = (axisMin - axisOrigin) * inverse;
                float second = (axisMax - axisOrigin) * inverse;
                if (first > second)
                {
                    (first, second) = (second, first);
                }
                near = Mathf.Max(near, first);
                far = Mathf.Min(far, second);
                if (far < near)
                {
                    return false;
                }
            }
            return far > 1e-6f;
        }

        private static bool RayIntersectsTriangle(
            Vector3 origin,
            Vector3 direction,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            out float distance)
        {
            distance = 0f;
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 p = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (Mathf.Abs(determinant) < 1e-8f)
            {
                return false;
            }

            float inverse = 1f / determinant;
            Vector3 t = origin - a;
            float u = Vector3.Dot(t, p) * inverse;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 q = Vector3.Cross(t, edge1);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            distance = Vector3.Dot(edge2, q) * inverse;
            return distance > 1e-6f;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static void CreateFolders(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
