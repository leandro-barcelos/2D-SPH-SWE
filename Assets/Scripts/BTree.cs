using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BTree
{
    private class Node
    {
        public List<int> val;
        public Node[] link;
        public Bounds bounds;

        public Node(Bounds bounds)
        {
            val = new();
            link = null;
            this.bounds = bounds;
        }

        public Node(List<int> val, Bounds domain)
        {
            this.val = val;
            link = null;
            this.bounds = domain;
        }

        public void CreateSubCells(int b)
        {
            link = new Node[b * b];
        }
    }

    // Consts
    private const float Alpha = 0.5f;
    private const float Beta = 0.75f;
    private const int Max = 2;

    private Vector2[] particlesPosition;
    private Node root;
    private int b;
    private int particleCount;

    public BTree(Bounds domainBounds, Vector2[] particlesPosition)
    {
        this.particlesPosition = particlesPosition;

        particleCount = particlesPosition.Length;

        List<int> val = Enumerable.Range(0, particleCount).ToList();

        root = new(val, domainBounds);

        BuildTree(root);
    }

    private void BuildTree(Node cell)
    {
        b = Mathf.CeilToInt(Mathf.Sqrt(cell.val.Count / Max));
        while (b >= 2)
        {
            cell.CreateSubCells(b);

            foreach (int p in cell.val)
            {
                var subcell = Vector2Int.FloorToInt((particlesPosition[p] - (Vector2)cell.bounds.min) / cell.bounds.size * b);

                var j = subcell.x + subcell.y * b;

                if (cell.link[j] == null)
                {
                    Vector2 subCellSize = cell.bounds.size / b;
                    var center = (Vector2)cell.bounds.min + subcell * subCellSize + subCellSize / 2;
                    Bounds bounds = new(center, subCellSize);
                    cell.link[j] = new(bounds);
                }

                cell.link[j].val.Add(p);
            }

            var count = 0;
            foreach (Node subCell in cell.link)
            {
                if (subCell == null || subCell.val.Count <= Alpha * Max)
                {
                    count++;
                }
            }

            var distributionRatio = (float)count / (b * b);

            if (distributionRatio > Beta)
                b /= 2;
            else
                break;
        }

        foreach (Node subCell in cell.link)
        {
            if (subCell == null) continue;
            BuildTree(subCell);
        }
    }

    // public List<int> FindNeighbors(int particle, float smoothingLength)
    // {
    //     var rxMin = Mathf.FloorToInt((particlesPosition[particle].x - 2 * smoothingLength - domainBounds.min.x) / (domainBounds.max.x - domainBounds.min.x) * b);
    //     var rxMax = Mathf.FloorToInt((particlesPosition[particle].x + 2 * smoothingLength - domainBounds.min.x) / (domainBounds.max.x - domainBounds.min.x) * b);

    //     var ryMin = Mathf.FloorToInt((particlesPosition[particle].y - 2 * smoothingLength - domainBounds.min.y) / (domainBounds.max.y - domainBounds.min.y) * b);
    //     var ryMax = Mathf.FloorToInt((particlesPosition[particle].y + 2 * smoothingLength - domainBounds.min.y) / (domainBounds.max.y - domainBounds.min.y) * b);

    //     for (int x = rxMin; x < rxMax; x++)
    //     {
    //         for (int y = ryMin; y < ryMin; y++)
    //         {
    //             var cellId = x + y * b;

    //             if (bTree[cellId].Count <= Max)
    //             {

    //             }
    //         }
    //     }
    // }
}
