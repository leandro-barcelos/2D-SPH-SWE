using System.Collections.Generic;
using UnityEngine;

public class GenerateCircleMesh
{

    private const int CircleSegmentCount = 12;
    private const int CircleVertexCount = CircleSegmentCount + 2;
    private const int CircleIndexCount = CircleSegmentCount * 3;

    public static Mesh GetMesh()
    {
        var circle = new Mesh();
        var vertices = new List<Vector3>(CircleVertexCount);
        var indices = new int[CircleIndexCount];
        var segmentWidth = Mathf.PI * 2f / CircleSegmentCount;
        var angle = 0f;
        vertices.Add(Vector3.zero);
        for (int i = 1; i < CircleVertexCount; ++i)
        {
            vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            angle -= segmentWidth;
            if (i > 1)
            {
                var j = (i - 2) * 3;
                indices[j + 0] = 0;
                indices[j + 1] = i - 1;
                indices[j + 2] = i;
            }
        }
        circle.SetVertices(vertices);
        circle.SetIndices(indices, MeshTopology.Triangles, 0);
        circle.RecalculateBounds();
        return circle;
    }
}
