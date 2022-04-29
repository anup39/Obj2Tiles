﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Obj2Tiles.Library.Materials;
using SixLabors.ImageSharp;

namespace Obj2Tiles.Library.Geometry;

public class Mesh : IMesh
{
    public readonly IReadOnlyList<Vertex3> Vertices;
    public readonly IReadOnlyList<Face<Vertex3>> Faces;

    public const string DefaultName = "Mesh";

    public string Name { get; set; } = DefaultName;

    public Mesh(IReadOnlyList<Vertex3> vertices, IReadOnlyList<Face<Vertex3>> faces)
    {
        Vertices = vertices;
        Faces = faces;
    }
    
    public int Split(IVertexUtils utils, double q, out IMesh left,
        out IMesh right)
    {
        var leftVertices = new Dictionary<Vertex3, int>(Vertices.Count);
        var rightVertices = new Dictionary<Vertex3, int>(Vertices.Count);

        var leftFaces = new List<Face<Vertex3>>(Faces.Count);
        var rightFaces = new List<Face<Vertex3>>(Faces.Count);

        var count = 0;

        for (var index = 0; index < Faces.Count; index++)
        {
            var face = Faces[index];
            var aSide = utils.GetDimension(face.A) < q;
            var bSide = utils.GetDimension(face.B) < q;
            var cSide = utils.GetDimension(face.C) < q;

            if (aSide)
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        // All on the left

                        var indexALeft = leftVertices.AddIndex(face.A);
                        var indexBLeft = leftVertices.AddIndex(face.B);
                        var indexCLeft = leftVertices.AddIndex(face.C);

                        leftFaces.Add(new Face<Vertex3>(indexALeft, indexBLeft, indexCLeft, face.A, face.B, face.C));
                    }
                    else
                    {
                        IntersectRight2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectRight2D(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
            }
            else
            {
                if (bSide)
                {
                    if (cSide)
                    {
                        IntersectRight2D(utils, q, face.IndexA, face.IndexB, face.IndexC, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        IntersectLeft2D(utils, q, face.IndexB, face.IndexC, face.IndexA, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                }
                else
                {
                    if (cSide)
                    {
                        IntersectLeft2D(utils, q, face.IndexC, face.IndexA, face.IndexB, leftVertices, rightVertices,
                            leftFaces, rightFaces);
                        count++;
                    }
                    else
                    {
                        // All on the right

                        var indexARight = rightVertices.AddIndex(face.A);
                        var indexBRight = rightVertices.AddIndex(face.B);
                        var indexCRight = rightVertices.AddIndex(face.C);
                        rightFaces.Add(
                            new Face<Vertex3>(indexARight, indexBRight, indexCRight, face.A, face.B, face.C));
                    }
                }
            }
        }

        var orderedLeftVertices = leftVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var orderedRightVertices = rightVertices.OrderBy(x => x.Value).Select(x => x.Key).ToList();

        left = new Mesh(orderedLeftVertices, leftFaces)
        {
            Name = $"{Name}-{utils.Axis}L"
        };

        right = new Mesh(orderedRightVertices, rightFaces)
        {
            Name = $"{Name}-{utils.Axis}R"
        };

        return count;
    }

    private void IntersectLeft2D(IVertexUtils utils, double q, int indexVL, int indexVR1, int indexVR2,
        IDictionary<Vertex3, int> leftVertices,
        IDictionary<Vertex3, int> rightVertices, ICollection<Face<Vertex3>> leftFaces,
        ICollection<Face<Vertex3>> rightFaces)
    {
        var vL = Vertices[indexVL];
        var vR1 = Vertices[indexVR1];
        var vR2 = Vertices[indexVR2];

        var indexVLLeft = leftVertices.AddIndex(vL);

        if (Math.Abs(utils.GetDimension(vR1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vR2) - q) < Common.Epsilon)
        {
            // Right Vertices are on the line

            var indexVR1Left = leftVertices.AddIndex(vR1);
            var indexVR2Left = leftVertices.AddIndex(vR2);

            leftFaces.Add(new Face<Vertex3>(indexVLLeft, indexVR1Left, indexVR2Left, vL, vR1, vR2));
            return;
        }

        var indexVR1Right = rightVertices.AddIndex(vR1);
        var indexVR2Right = rightVertices.AddIndex(vR2);

        // a on the left, b and c on the right

        // Prima intersezione
        var t1 = utils.CutEdge(vL, vR1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // Seconda intersezione
        var t2 = utils.CutEdge(vL, vR2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        var lface = new Face<Vertex3>(indexVLLeft, indexT1Left, indexT2Left, vL, t1, t2);
        leftFaces.Add(lface);

        var rface1 = new Face<Vertex3>(indexT1Right, indexVR1Right, indexVR2Right, t1, vR1, vR2);
        rightFaces.Add(rface1);

        var rface2 = new Face<Vertex3>(indexT1Right, indexVR2Right, indexT2Right, t1, vR2, t2);
        rightFaces.Add(rface2);
    }

    private void IntersectRight2D(IVertexUtils utils, double q, int indexVR, int indexVL1, int indexVL2,
        IDictionary<Vertex3, int> leftVertices, IDictionary<Vertex3, int> rightVertices,
        ICollection<Face<Vertex3>> leftFaces, ICollection<Face<Vertex3>> rightFaces)
    {
        var vR = Vertices[indexVR];
        var vL1 = Vertices[indexVL1];
        var vL2 = Vertices[indexVL2];

        var indexVRRight = rightVertices.AddIndex(vR);

        if (Math.Abs(utils.GetDimension(vL1) - q) < Common.Epsilon &&
            Math.Abs(utils.GetDimension(vL2) - q) < Common.Epsilon)
        {
            // Left Vertices are on the line
            var indexVL1Right = rightVertices.AddIndex(vL1);
            var indexVL2Right = rightVertices.AddIndex(vL2);

            rightFaces.Add(new Face<Vertex3>(indexVRRight, indexVL1Right, indexVL2Right, vR, vL1, vL2));

            return;
        }

        var indexVL1Left = leftVertices.AddIndex(vL1);
        var indexVL2Left = leftVertices.AddIndex(vL2);

        // a on the right, b and c on the left

        // Prima intersezione
        var t1 = utils.CutEdge(vR, vL1, q);
        var indexT1Left = leftVertices.AddIndex(t1);
        var indexT1Right = rightVertices.AddIndex(t1);

        // Seconda intersezione
        var t2 = utils.CutEdge(vR, vL2, q);
        var indexT2Left = leftVertices.AddIndex(t2);
        var indexT2Right = rightVertices.AddIndex(t2);

        var rface = new Face<Vertex3>(indexVRRight, indexT1Right, indexT2Right, vR, t1, t2);
        rightFaces.Add(rface);

        var lface1 = new Face<Vertex3>(indexT2Left, indexVL1Left, indexVL2Left, t2, vL1, vL2);
        leftFaces.Add(lface1);

        var lface2 = new Face<Vertex3>(indexT2Left, indexT1Left, indexVL1Left, t2, t1, vL1);
        leftFaces.Add(lface2);
    }

    #region Utils

    public Box3 Bounds
    {
        get
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var minZ = double.MaxValue;

            var maxX = double.MinValue;
            var maxY = double.MinValue;
            var maxZ = double.MinValue;

            foreach (var v in Vertices)
            {
                minX = minX < v.X ? minX : v.X;
                minY = minY < v.Y ? minY : v.Y;
                minZ = minZ < v.Z ? minZ : v.Z;

                maxX = v.X > maxX ? maxX : v.X;
                maxY = v.Y > maxY ? maxY : v.Y;
                maxZ = v.Z > maxZ ? maxZ : v.Z;
            }

            return new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }
    }

    public Vertex3 GetVertexBaricenter()
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;

        for (var index = 0; index < Vertices.Count; index++)
        {
            var v = Vertices[index];
            x += v.X;
            y += v.Y;
            z += v.Z;
        }

        x /= Vertices.Count;
        y /= Vertices.Count;
        z /= Vertices.Count;

        return new Vertex3(x, y, z);
    }

    public void WriteObj(string path)
    {
        using var writer = new FormattingStreamWriter(path, CultureInfo.GetCultureInfo("en-US"));
        
        writer.Write("o ");
        writer.WriteLine(string.IsNullOrWhiteSpace(Name) ? DefaultName : Name);

        foreach (var vertex in Vertices)
        {
            writer.Write("v ");
            writer.Write(vertex.X);
            writer.Write(" ");
            writer.Write(vertex.Y);
            writer.Write(" ");
            writer.WriteLine(vertex.Z);
        }

        foreach (var face in Faces)
        {
            writer.WriteLine(face.ToObj());
        }
    }

    public int FacesCount => Faces.Count;
    public int VertexCount => Vertices.Count;

    #endregion


}