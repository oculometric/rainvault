using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class AsteroidManager : MeshInstance3D
{
    [Export] private CpuParticles3D particles;
    [Export] private CollisionShape3D collider;

    private Dictionary<Vector3, bool> all_points = new Dictionary<Vector3, bool>(); // TODO: convert this to a pair of arrays so we dont have to iterate a lot
    private LinkedList<Vector3> unscanned_points = new LinkedList<Vector3>();
    private LinkedList<Vector3> unscanned_normals = new LinkedList<Vector3>();

    Vector3 seed = Vector3.Zero;
    float AsteroidNoise(Vector3 v)
    {
        return 80.0f - (((v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z))) + (NoiseHelper.fbm((v / 10.0f) + seed, 6, 2.0f, 0.5f) * 80.0f);
    }

    public override void _Ready()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();
        //rng.Seed = (ulong)(Time.GetUnixTimeFromSystem() * 10000.0f);
        seed = new Vector3(rng.Randf(), rng.Randf(), rng.Randf()) * 1000.0f;
        Mesh = MeshVoxeliser.GenerateMesh(AsteroidNoise, 0.0f, 1.0f, 1.414f, Vector3.Zero, Vector3.One * 24.0f);
        ArrayMesh am = Mesh as ArrayMesh;
        Vector3[] verts = am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector3[] norms = am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Normal].AsVector3Array();
        int[] inds = am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Index].AsInt32Array();
        for (int i = 0; i < verts.Length; i++)
            all_points[verts[i] + (norms[i] * 0.8f)] = false;

        Vector3[] collision_verts = new Vector3[inds.Length];
        for (int i = 0; i < inds.Length; i++)
            collision_verts[i] = verts[inds[i]];

        (collider.Shape as ConcavePolygonShape3D).SetFaces(collision_verts);
        unscanned_points = new LinkedList<Vector3>(all_points.Keys);
        unscanned_normals = new LinkedList<Vector3>(norms);

        particles.EmissionPoints = unscanned_points.ToArray();
        particles.EmissionNormals = unscanned_normals.ToArray();
    }

    public void ScanRay(Vector3 origin, Vector3 direction, float radius)
    {
        GD.Print("scanning...");
        PhysicsRayQueryParameters3D param = new PhysicsRayQueryParameters3D();
        param.From = origin;
        param.To = param.From + (direction * 300.0f);
        Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(param);

        if (res.Count == 0)
            return;

        Vector3 local_pos = GlobalBasis.Inverse() * res["position"].AsVector3();
        GD.Print("searching...");
        Vector3 closest = Vector3.Inf;
        Vector3 closest_normal = Vector3.Zero;
        LinkedListNode<Vector3> normal_node = unscanned_normals.First;
        float min_dist = float.PositiveInfinity;
        int closest_index = 0;
        int index = 0;
        foreach (Vector3 v in unscanned_points)
        {
            float dist = v.DistanceSquaredTo(local_pos);
            if (dist < min_dist)
            {
                min_dist = dist;
                closest = v;
                closest_normal = normal_node.Value;
                closest_index = index;
            }
            normal_node = normal_node.Next;
            index++;
        }
        if (min_dist < (radius * radius))
        {
            unscanned_points.Remove(closest);
            unscanned_normals.Remove(closest_normal);
            all_points[closest] = true;
            GD.Print(closest + " has been scanned");
            particles.EmissionPoints = unscanned_points.ToArray();
            particles.EmissionNormals = unscanned_normals.ToArray();
        }
    }
}
