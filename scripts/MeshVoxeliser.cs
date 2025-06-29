using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
public partial class MeshVoxeliser : Node3D
{
    public override void _Ready()
    {
        RegenerateMesh();
    }

    [ExportToolButton("Regenerate Mesh")]
    public Callable GenerateMeshButton => Callable.From(RegenerateMesh);
    [Export(PropertyHint.Range, "0.0001,10,0.01,or_greater")]
    public float resolution { get => _resolution; set { _resolution = value; RegenerateMesh(); } }
    private float _resolution = 0.0625f;
    [Export]
    public float threshold { get => _threshold; set { _threshold = value; RegenerateMesh(); } }
    private float _threshold = 0.0f;
    [Export]
    public Vector3 offset { get => _offset; set { _offset = value; RegenerateMesh(); } }
    private Vector3 _offset = Vector3.Zero;
    [Export]
    public Vector3 size { get => _size; set { _size = value; RegenerateMesh(); } }
    private Vector3 _size = Vector3.One * 3.0f;
    [Export]
    public float merge_distance_factor { get => _merge_distance_factor; set { _merge_distance_factor = value; RegenerateMesh(); } }
    private float _merge_distance_factor = 1.414f;

    [Export]
    public MeshInstance3D target;

    void RegenerateMesh()
    {
        //List<Tuple<float, float, float>> timings = new List<Tuple<float, float, float>>();
        if (target != null)
            target.Mesh = GenerateMesh((position) => 1.0f - ((position.X * position.X) + (position.Y * position.Y) + (position.Z * position.Z)),
                threshold, resolution, merge_distance_factor, offset, size);//, ref timings);
        //if (!Engine.IsEditorHint())
        //{
        //    for (int i = 0; i < 20; i++)
        //        GenerateMesh((position) => 1.0f - ((position.X * position.X) + (position.Y * position.Y) + (position.Z * position.Z)),
        //            0.0f, 0.0625f, merge_distance_factor, Vector3.Zero, Vector3.One * 3.0f);//, ref timings);

        //    //float total_eval = 0.0f; float total_gen = 0.0f; float total_clean = 0.0f;
        //    //foreach (Tuple<float, float, float> t in timings)
        //    //{
        //    //    total_eval += t.Item1;
        //    //    total_gen += t.Item2;
        //    //    total_clean += t.Item3;
        //    //}
        //    //total_eval /= timings.Count;
        //    //total_gen /= timings.Count;
        //    //total_clean /= timings.Count;
        //    //GD.Print("========> timing data for 20 iterations:");
        //    //GD.Print("=> eval: " + total_eval);
        //    //GD.Print("=> gen: " + total_gen);
        //    //GD.Print("=> clean: " + total_clean);
        //    //GD.Print("==> total: " + (total_eval + total_gen + total_clean));
        //    //GD.Print("(for sphere demo)");
        //}
    }

    struct VoxelMap
    {
        private Vector3I size;
        private int yStep;
        private int zStep;
        private float[] value_map;
        private bool[] threshold_map;

        public VoxelMap(Vector3I voxel_count)
        {
            size = voxel_count; yStep = voxel_count.X; zStep = voxel_count.X * voxel_count.Y;
            value_map = new float[voxel_count.X * voxel_count.Y * voxel_count.Z];
            threshold_map = new bool[value_map.Length];
        }

        public void Write(int index, float value, bool threshold)
        {
            value_map[index] = value;
            threshold_map[index] = threshold;
        }

        public bool ReadThreshold(int x, int y, int z)
        {
            return threshold_map[x + (y * yStep) + (z * zStep)];
        }

        public void ReadData(int x, int y, int z, out bool threshold, out float value)
        {
            int index = x + (y * yStep) + (z * zStep);
            threshold = threshold_map[index];
            value = value_map[index];
        }

        public Vector3I GetSize()
        {
            return size;
        }
    }

    public static ArrayMesh GenerateMesh(Func<Vector3, float> field_function, float threshold, float voxel_scale, float merge_distance_factor, Vector3 space_offset, Vector3 space_size)//, ref List<Tuple<float, float, float>> timings)
    {
        GD.Print("beginning MT mesh generation!!");

        Stopwatch eval_timer = Stopwatch.StartNew();

        // pre-evaluate all grid values (prevents recompuation)
        Vector3I voxel_count = (Vector3I)(space_size / voxel_scale).Ceil();
        Vector3 voxel_range = (Vector3)voxel_count * voxel_scale / 2.0f;
        VoxelMap voxel_map = new VoxelMap(voxel_count + Vector3I.One);

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
                    float val = field_function(voxel);
                    voxel_map.Write(index, val, val > threshold);

                    voxel.X += voxel_scale;
                    index++;
                }

                voxel.Y += voxel_scale;
            }

            voxel.Z += voxel_scale;
        }

        eval_timer.Stop();
        GD.Print("evaluated voxel map with size " + voxel_map.GetSize());

        Stopwatch gen_timer = Stopwatch.StartNew();

        List<Vector3> vertices = new List<Vector3>();
        // march through the function
        voxel = Vector3.Zero;
        voxel.Z = -voxel_range.Z;
        for (int z = 0; z < voxel_count.Z; z++)
        {
            voxel.Y = -voxel_range.Y;
            for (int y = 0; y < voxel_count.Y; y++)
            {
                voxel.X = -voxel_range.X;
                for (int x = 0; x < voxel_count.X; x++)
                {
                    // extract the corner values from the generated array
                    bool tmp = false;
                    byte corner_values = 0;
                    float[] corners = new float[8];

                    voxel_map.ReadData(x + 1, y + 1, z + 1, out tmp, out corners[0]);
                    if (tmp) corner_values |= 0b00000001;
                    voxel_map.ReadData(x, y + 1, z + 1, out tmp, out corners[1]);
                    if (tmp) corner_values |= 0b00000010;
                    voxel_map.ReadData(x + 1, y, z + 1, out tmp, out corners[2]);
                    if (tmp) corner_values |= 0b00000100;
                    voxel_map.ReadData(x, y, z + 1, out tmp, out corners[3]);
                    if (tmp) corner_values |= 0b00001000;
                    voxel_map.ReadData(x + 1, y + 1, z, out tmp, out corners[4]);
                    if (tmp) corner_values |= 0b00010000;
                    voxel_map.ReadData(x, y + 1, z, out tmp, out corners[5]);
                    if (tmp) corner_values |= 0b00100000;
                    voxel_map.ReadData(x + 1, y, z, out tmp, out corners[6]);
                    if (tmp) corner_values |= 0b01000000;
                    voxel_map.ReadData(x, y, z, out tmp, out corners[7]);
                    if (tmp) corner_values |= 0b10000000;

                    GenerateCubeGeometry(ref vertices, voxel, voxel_scale, new Tuple<byte, float[]>(corner_values, corners), threshold);
                    voxel.X += voxel_scale;
                }
                voxel.Y += voxel_scale;
            }
            voxel.Z += voxel_scale;
        }

        gen_timer.Stop();
        GD.Print("have a raw mesh with " + vertices.Count + " verts.");

        Stopwatch clean_timer = Stopwatch.StartNew();

        List<Vector3> verts;
        List<int> inds;
        DistanceCollapse(out verts, out inds, vertices, voxel_scale * merge_distance_factor);
        List<Vector3> norms;
        GenerateSmoothNormals(in verts, in inds, out norms);

        clean_timer.Stop();
        GD.Print("generated clean mesh with " + verts.Count + " verts and " + inds.Count + " indices.");

        GD.Print("generated voxel map of size " + voxel_count + "(" + (voxel_count.X * voxel_count.Y * voxel_count.Z) + ") in " + (eval_timer.Elapsed + gen_timer.Elapsed + clean_timer.Elapsed).TotalSeconds + " seconds");
        GD.Print("eval: " + eval_timer.Elapsed.TotalSeconds + "; gen: " + gen_timer.Elapsed.TotalSeconds + "; clean: " + clean_timer.Elapsed.TotalSeconds);
        //timings.Add(new Tuple<float, float, float>((float)eval_timer.Elapsed.TotalSeconds, (float)gen_timer.Elapsed.TotalSeconds, (float)clean_timer.Elapsed.TotalSeconds));

        // generate arraymesh
        Godot.Collections.Array surface_array = [];
        surface_array.Resize((int)Mesh.ArrayType.Max);
        surface_array[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        surface_array[(int)Mesh.ArrayType.Normal] = norms.ToArray();
        surface_array[(int)Mesh.ArrayType.TexUV] = new Vector2[verts.Count];
        surface_array[(int)Mesh.ArrayType.Index] = inds.ToArray();
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

    static Tuple<Vector3, bool, float>[] ExtractCorners(Vector3 center, float voxel_scale, Tuple<byte, float[]> values, int[] indices)
    {
        Tuple<Vector3, bool, float>[] arr = new Tuple<Vector3, bool, float>[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            arr[i] = new Tuple<Vector3, bool, float>
                (center + (cube_corners[indices[i]] * voxel_scale * 0.5f),
                (values.Item1 & (0b1 << indices[i])) > 0,
                values.Item2[indices[i]]);
        }

        return arr;
    }

    static void GenerateCubeGeometry(ref List<Vector3> vertices, Vector3 cube_center, float cube_size, Tuple<byte, float[]> corner_values, float threshold)
    {
        // evaluate the corners of the cube
        //byte corner_values = EvaluateScalarField(cube_center, field_function, threshold);
        // if the entire cube is inside or outside the field, there is no geometry to generate
        if (corner_values.Item1 == 0b00000000 || corner_values.Item1 == 0b11111111)
            return;

        // generate geometry for each of the 6 tetrahedra inside the cube

        // tetra0 - 0, 4, 2, 3
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, cube_size, corner_values, [0, 4, 2, 3]), threshold);
        // tetra1 - 3, 7, 5, 4
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, cube_size, corner_values, [3, 7, 5, 4]), threshold);
        // tetra2 - 1, 5, 4, 3
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, cube_size, corner_values, [1, 5, 4, 3]), threshold);
        // tetra3 - 2, 6, 3, 4
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, cube_size, corner_values, [2, 6, 3, 4]), threshold);
        // tetra4 - 0, 3, 1, 4
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, cube_size, corner_values, [0, 3, 1, 4]), threshold);
        // tetra5 - 4, 7, 6, 3
        GenerateTetraGeometry(ref vertices, ExtractCorners(cube_center, cube_size, corner_values, [4, 7, 6, 3]), threshold);
    }

    const int edge_01 = 0;
    const int edge_12 = 1;
    const int edge_20 = 2;
    const int edge_03 = 3;
    const int edge_13 = 4;
    const int edge_23 = 5;
    static readonly int[] edge_vs =
    {
        0, 1,
        1, 2,
        2, 0,
        0, 3,
        1, 3,
        2, 3
    };
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

    static void GenerateTetraGeometry(ref List<Vector3> vertices, Tuple<Vector3, bool, float>[] corners, float threshold)
    {
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

        // look up which sequence of vertices are to be used based on the evaluated sign changes
        int[] vertex_selection = tetrahedron_patterns[corner_pattern];

        // based on the lookup, compute interpolated vertex positions using the value data of the voxel field
        foreach (int i in vertex_selection)
        {
            int i1 = edge_vs[(i * 2) + 0];
            int i2 = edge_vs[(i * 2) + 1];
            Vector3 v1 = corners[i1].Item1;
            Vector3 v2 = corners[i2].Item1;
            float f1 = corners[i1].Item3;
            float f2 = corners[i2].Item3;

            float f = (threshold - f1) / (f2 - f1);
            vertices.Add((f * v2) + ((1.0f - f) * v1));
        }    
    }

    static void DistanceCollapse(out List<Vector3> result_vertices, out List<int> result_indices, List<Vector3> input_vertices, float distance)
    {
        result_vertices = new List<Vector3>();
        result_vertices.EnsureCapacity(input_vertices.Count / 6);
        result_indices = new List<int>();
        result_indices.EnsureCapacity(input_vertices.Count);
        float d2 = distance * distance;

        // convert a raw vertex array to a pair of (deduplicated) vertex and index arrays
        Dictionary<Vector3, int> vertex_refs = new Dictionary<Vector3, int>(/*new VectorComparer()*/);
        vertex_refs.EnsureCapacity(input_vertices.Count / 3);
        LinkedList<Vector3> unmarked_verts = new LinkedList<Vector3>();

        // add all used vertices to vertex refs dictionary.
        foreach (Vector3 vertex in input_vertices)
            vertex_refs[vertex] = -1;
        foreach (KeyValuePair<Vector3, int> vertex in vertex_refs)
            unmarked_verts.AddLast(vertex.Key);

        // perform proximity clustering
        while (unmarked_verts.Count > 0)
        {
            // select unmarked vertex, pointing its index to where the new vertex will be
            Vector3 start_vert = unmarked_verts.First.Value;
            int index = result_vertices.Count;
            vertex_refs[start_vert] = index;
            unmarked_verts.RemoveFirst();

            // collect a cluster of nearby verts, marking each as used
            Vector3 sum_position = start_vert;
            int total = 1;
            LinkedListNode<Vector3> lln = unmarked_verts.First;
            while (lln != null)
            {
                // calculate individual differences to perform early rejection
                float dx = lln.Value.X - start_vert.X;
                if (dx > distance) { lln = lln.Next; continue; }
                if (dx < -distance) { lln = lln.Next; continue; }
                float dy = lln.Value.Y - start_vert.Y;
                if (dy > distance) { lln = lln.Next; continue; }
                if (dy < -distance) { lln = lln.Next; continue; }
                float dz = lln.Value.Z - start_vert.Z;
                if (dz > distance) { lln = lln.Next; continue; }
                if (dz < -distance) { lln = lln.Next; continue; }
                if (((dx * dx) + (dy * dy) + (dz * dz)) < d2)
                {
                    // set the vertex ref for this vertex position to point to
                    // the last vertex in the result vertex array
                    vertex_refs[lln.Value] = index;
                    // contribute to the position sum
                    sum_position += lln.Value;
                    // increment the number of vertices clustered
                    total++;
                    // remove this node from the linked list
                    LinkedListNode<Vector3> old = lln;
                    lln = lln.Next;
                    unmarked_verts.Remove(old);
                }
                else
                    lln = lln.Next;
            }

            // create the resulting vertex
            result_vertices.Add(((sum_position / total) + start_vert) * 0.5f);
        }

        // build the index array
        int r = 0;
        int i = 0;
        foreach (Vector3 vertex in input_vertices)
        {
            int index = vertex_refs[vertex];
            result_indices.Add(index);
            i++;

            if (index < 0 || index >= result_vertices.Count)
                GD.Print("oops");

            if (i == 3)
            {
                i = 0;
                int i0 = result_indices[result_indices.Count - 3];
                int i1 = result_indices[result_indices.Count - 2];
                int i2 = result_indices[result_indices.Count - 1];

                if (i0 == i1 || i0 == i2 || i1 == i2)
                {
                    result_indices.RemoveRange(result_indices.Count - 3, 3);
                    r++;
                }
            }
        }
        GD.Print("removed " + r + " degenerate faces aka " + (r * 3) + " indices");
    }

    static void GenerateSmoothNormals(in List<Vector3> vertices, in List<int> indices, out List<Vector3> normals)
    {
        normals = new List<Vector3>();
        Vector3[] normals_sum = new Vector3[vertices.Count];
        int[] normals_num = new int[vertices.Count];

        // calculate normals for each face
        Vector3[] face_norms = new Vector3[indices.Count / 3];
        int j = 0;
        for (int i = 0; i < face_norms.Length; i++)
        {
            int i0 = indices[j];
            int i1 = indices[j + 1];
            int i2 = indices[j + 2];
            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 norm = (v2 - v0).Cross(v1 - v0).Normalized();
            face_norms[i] = norm;

            // add normal to vertices which use it
            normals_sum[i0] += norm; normals_num[i0]++;
            normals_sum[i1] += norm; normals_num[i1]++;
            normals_sum[i2] += norm; normals_num[i2]++;

            j += 3;
        }

        // divide normals to average out
        normals.EnsureCapacity(vertices.Count);
        for (int i = 0; i < normals_num.Length; i++)
            normals.Add(normals_sum[i] / normals_num[i]);
    }
}
