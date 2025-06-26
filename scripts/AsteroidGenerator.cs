using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/*
 * 
 * byte representation of cube corner:
 * 
 *   |--- -x -y -z
 *   | |--- -x +y -z
 *   | | |--- -x -y +z 
 *   | | | |--- -x +y +z
 *   | | | |
 * 0b00000000
 *    ^ ^ ^ ^
 *    | | | |--- +x +y +z
 *    | | |--- +x -y +z
 *    | |--- +x +y -z
 *    |--- +x -y -z
 */

[Tool]
public partial class AsteroidGenerator : MeshInstance3D
{
    public override void _Ready()
    {
        RegenerateMesh();
    }

    [ExportToolButton("Regenerate Mesh")]
    public Callable GenerateMeshButton => Callable.From(RegenerateMesh);

    void RegenerateMesh()
    {
        Mesh = GenerateMesh((position) => 1.0f - (((position.X * position.X) + (position.Y * position.Y) + (position.Z * position.Z)) * (1.0f / 550.0f)),
            0.5f, 0.25f, Vector3.Zero, Vector3.One * 40.0f);
    }

    public static ArrayMesh GenerateMesh(Func<Vector3, float> field_function, float threshold, float voxel_scale, Vector3 space_offset, Vector3 space_size)
    {
        // pre-evaluate all grid values (prevents recompuation)
        Vector3 voxel_range = (space_size / voxel_scale).Ceil() / 2.0f;
        Vector3I voxel_count = (Vector3I)(voxel_range * 2.0f);
        float[] value_map = new float[(voxel_count.X + 1) * (voxel_count.Y + 1) * (voxel_count.Z + 1)];
        bool[] threshold_map = new bool[value_map.Length];

        int index = 0;
        Vector3 voxel = Vector3.Zero;
        voxel.Z = (space_offset.Z - voxel_range.Z) - (voxel_scale / 2.0f);
        for (int z = 0; z <= voxel_count.Z; z++)
        {
            voxel.Y = (space_offset.Y - voxel_range.Y) - (voxel_scale / 2.0f);
            for (int y = 0; y <= voxel_count.Y; y++)
            {
                voxel.X = (space_offset.X - voxel_range.X) - (voxel_scale / 2.0f);
                for (int x = 0; x <= voxel_count.X; x++)
                {
                    value_map[index] = field_function(voxel);
                    threshold_map[index] = value_map[index] > threshold;

                    voxel.X += voxel_scale;
                    index++;
                }

                voxel.Y += voxel_scale;
            }

            voxel.Z += voxel_scale;
        }

        List<Vector3> vertices = new List<Vector3>();
        // march through the function
        voxel = Vector3.Zero;
        voxel.Z = space_offset.Z - voxel_range.Z;
        for (int z = 0; z < voxel_count.Z; z++)
        {
            voxel.Y = space_offset.Y - voxel_range.Y;
            for (int y = 0; y < voxel_count.Y; y++)
            {
                voxel.X = space_offset.X - voxel_range.X;
                for (int x = 0; x < voxel_count.X; x++)
                {
                    byte corner_values = 0;
                    // TODO: extract the corner values from the generated array...
                    GenerateCubeGeometry(ref vertices, new Vector3(x, y, z), voxel_scale);
                    voxel.X += voxel_scale;
                }
                voxel.Y += voxel_scale;
            }
            voxel.Z += voxel_scale;
        }

        GD.Print("have a raw mesh with " + vertices.Count + " verts.");

        // convert a raw vertex array to a pair of (deduplicated) vertex and index arrays
        Dictionary<Vector3, int> vertex_refs = new Dictionary<Vector3, int>();
        List<Vector3> used_vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        foreach (Vector3 vertex in vertices)
        {
            int i;
            // if the vertex is not present, add it to the refmap and the new vertex array
            if (!vertex_refs.TryGetValue(vertex, out i))
            {
                i = used_vertices.Count;
                used_vertices.Add(vertex);
                vertex_refs[vertex] = i;
            }
            
            // add the index to the correct vertex
            indices.Add(i);
        }

        GD.Print("converted raw to clean mesh with " + used_vertices.Count + " verts and " + indices.Count + " indices.");

        // TODO: merge by distance

        // generate arraymesh
        Godot.Collections.Array surface_array = [];
        surface_array.Resize((int)Mesh.ArrayType.Max);
        surface_array[(int)Mesh.ArrayType.Vertex] = used_vertices.ToArray();
        surface_array[(int)Mesh.ArrayType.Normal] = new Vector3[used_vertices.Count];
        surface_array[(int)Mesh.ArrayType.TexUV] = new Vector2[used_vertices.Count];
        surface_array[(int)Mesh.ArrayType.Index] = indices.ToArray();
        ArrayMesh new_mesh = new ArrayMesh();
        new_mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surface_array);

        return new_mesh;
    }

    static readonly Vector3[] cube_corners =
    {
        new( 1,  1,  1), // 0
        new(-1,  1,  1), // 1
        new( 1, -1,  1), // 2
        new(-1, -1,  1), // 3
        new( 1,  1, -1), // 4
        new(-1,  1, -1), // 5
        new( 1, -1, -1), // 6
        new(-1, -1, -1)  // 7
    };

    static Tuple<Vector3, bool> GetCorner(Vector3 center, byte values, int index)
    {
        return new Tuple<Vector3, bool>(center + cube_corners[index], (values & (0b1 << index)) > 0);
    }

    static Tuple<Vector3, bool>[] ExtractCorners(Vector3 center, byte values, int[] indices)
    {
        Tuple<Vector3, bool>[] arr = new Tuple<Vector3, bool>[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            arr[i] = GetCorner(center, values, indices[i]);

        return arr;
    }

    static void GenerateCubeGeometry(ref List<Vector3> vertices, Vector3 cube_center, float cube_size, byte corner_values)
    {
        // evaluate the corners of the cube
        byte corner_values = EvaluateScalarField(cube_center, field_function, threshold);
        // if the entire cube is inside or outside the field, there is no geometry to generate
        if (corner_values == 0b00000000 || corner_values == 0b11111111)
            return;

        // generate geometry for each of the 6 tetrahedra inside the cube

        // tetra0 - 0, 4, 2, 3
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, corner_values, [0, 4, 2, 3]));
        // tetra1 - 3, 7, 5, 4
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, corner_values, [3, 7, 5, 4]));
        // tetra2 - 1, 5, 4, 3
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, corner_values, [1, 5, 4, 3]));
        // tetra3 - 2, 6, 3, 4
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, corner_values, [2, 6, 3, 4]));
        // tetra4 - 0, 3, 1, 4
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, corner_values, [0, 3, 1, 4]));
        // tetra5 - 4, 7, 6, 3
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, corner_values, [4, 7, 6, 3]));
    }

    static readonly int edge_01 = 0;
    static readonly int edge_12 = 1;
    static readonly int edge_20 = 2;
    static readonly int edge_03 = 3;
    static readonly int edge_13 = 4;
    static readonly int edge_23 = 5;
    static readonly int[][] tetrahedron_patterns =
    {
        [ ],                                                        // pattern 0000 (i.e. all corners outside)
        [ edge_01, edge_03, edge_20 ],                              // pattern 0001 (i.e. corner 0 inside)
        [ edge_12, edge_13, edge_01 ],                              // pattern 0010 (i.e. corner 1 inside)
        [ edge_12, edge_13, edge_03, edge_12, edge_03, edge_20 ],   // pattern 0011 (i.e. corners 0 and 1 inside)
        [ edge_12, edge_20, edge_23 ],                              // pattern 0100 (i.e. corner 2 inside)
        [ edge_01, edge_03, edge_12, edge_12, edge_03, edge_23 ],   // pattern 0101 (i.e. corners 0 and 2 inside)
        [ edge_13, edge_01, edge_20, edge_13, edge_20, edge_23 ],   // pattern 0110 (i.e. corners 1 and 2 inside)
        [ edge_13, edge_03, edge_23 ],                              // pattern 0111 (i.e. corners 0, 1, and 2 inside)
        [ edge_13, edge_23, edge_03 ],                              // pattern 1000 (i.e. corner 3 inside)
        [ edge_01, edge_13, edge_23, edge_01, edge_23, edge_20 ],   // pattern 1001 (i.e. corners 0 and 3 inside)
        [ edge_01, edge_12, edge_23, edge_01, edge_23, edge_03 ],   // pattern 1010 (i.e. corners 1 and 3 inside)
        [ edge_12, edge_23, edge_20 ],                              // pattern 1011 (i.e. corners 0, 1, and 3 inside)
        [ edge_13, edge_12, edge_20, edge_13, edge_20, edge_03 ],   // pattern 1100 (i.e. corners 2 and 3 inside)
        [ edge_01, edge_13, edge_12 ],                              // pattern 1101 (i.e. corners 0, 2, and 3 inside)
        [ edge_01, edge_20, edge_03 ],                              // pattern 1110 (i.e. corners 1, 2, and 3 inside)
        [ ],                                                        // pattern 1111 (i.e. all corners inside)
    };

    static void GenerateTetraGeometry(ref List<Vector3> vertices, Tuple<Vector3, bool>[] corners)
    {
        // TODO: interpolation

        // this functions expects an array of four pairs of corner position and field presence
        // these should be ordered clockwise for one of the faces, followed by the fourth corner

        // edges are organised in the following corner pair order: 01, 12, 20, 03, 13, 23

        // if all the corners are inside or outside the field, there is no geometry to generate
        int corner_pattern = (corners[3].Item2 ? 0b1000 : 0)
                           | (corners[2].Item2 ? 0b0100 : 0)
                           | (corners[1].Item2 ? 0b0010 : 0)
                           | (corners[0].Item2 ? 0b0001 : 0);

        if (corner_pattern == 0b00000000 || corner_pattern == 0b11111111)
            return;

        // calculate the positions of the vertex candidates to be placed on each edge
        Vector3[] candidates = new Vector3[6];
        candidates[edge_01] = (corners[0].Item1 + corners[1].Item1) / 2;
        candidates[edge_12] = (corners[1].Item1 + corners[2].Item1) / 2;
        candidates[edge_20] = (corners[2].Item1 + corners[0].Item1) / 2;
        candidates[edge_03] = (corners[0].Item1 + corners[3].Item1) / 2;
        candidates[edge_13] = (corners[1].Item1 + corners[3].Item1) / 2;
        candidates[edge_23] = (corners[2].Item1 + corners[3].Item1) / 2;

        // look up which sequence of vertices are to be used based on the evaluated sign changes
        int[] vertex_selection = tetrahedron_patterns[corner_pattern];

        // based on the lookup, add a selection of computed vertex candidates to the vertex array
        foreach (int i in vertex_selection)
            vertices.Add(candidates[i]);
    }
    static byte EvaluateScalarField(Vector3 cube_center, Func<Vector3, float> field_function, float threshold)
    {
        byte corner_eval = 0;
        for(int i = 0; i < 8; i++)
        {
            if (field_function(cube_center + cube_corners[i]) > threshold)
                corner_eval = (byte)(corner_eval | (0b1 << i));
        }

        GD.Print(cube_center + " => " + corner_eval);

        return corner_eval;
    }
}
