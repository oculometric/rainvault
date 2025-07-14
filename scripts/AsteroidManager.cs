using Godot;
using System.Collections.Generic;

[Tool]
public partial class AsteroidManager : MeshInstance3D
{
    [Export] private CollisionShape3D collider;
    [Export] private Mesh error_chunk_mesh;

    private Dictionary<Vector3, MeshInstance3D> all_points = new Dictionary<Vector3, MeshInstance3D>(); // TODO: convert this to a pair of arrays so we dont have to iterate a lot

    Vector3 seed = Vector3.Zero;
    Vector3 shape = Vector3.Zero;
    float AsteroidNoise(Vector3 v)
    {
        return 80.0f 
            - (((v.X * v.X / shape.X) + (v.Y * v.Y / shape.Y) + (v.Z * v.Z / shape.Z)))
            + (NoiseHelper.fbm((v / 20.0f) + seed, 6, 1.5f, 0.8f) * 150.0f)
            + ((Mathf.Pow(NoiseHelper.vor((v / 5.0f) - seed, 0.8f), 3.0f) - 0.1f) * 100.0f);
    }

    [ExportToolButton("Regenerate Mesh")]
    public Callable GenerateMeshButton => Callable.From(_Ready);
    public override void _Ready()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();
        seed = new Vector3(rng.Randf(), rng.Randf(), rng.Randf()) * 1000.0f;
        shape = new Vector3(rng.RandfRange(0.35f, 1.0f), rng.RandfRange(0.35f, 1.0f), rng.RandfRange(0.35f, 1.0f));
        shape = shape.Normalized();
        // generate mesh
        Mesh = MeshVoxeliser.GenerateMesh(AsteroidNoise, 0.0f, 1.0f, 0.814f, Vector3.Zero, Vector3.One * 24.0f);
        ArrayMesh am = Mesh as ArrayMesh;
        Vector3[] verts = am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector3[] norms = am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Normal].AsVector3Array();
        int[] inds = am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Index].AsInt32Array();
        // generate collision shape
        Vector3[] collision_verts = new Vector3[inds.Length];
        for (int i = 0; i < inds.Length; i++)
            collision_verts[i] = verts[inds[i]];
        (collider.Shape as ConcavePolygonShape3D).SetFaces(collision_verts);

        if (Engine.IsEditorHint())
            return;

        // generate coverage points
        for (int i = 0; i < verts.Length; i++)
        {
            MeshInstance3D mi = new();
            AddChild(mi);
            mi.Mesh = error_chunk_mesh;
            mi.CastShadow = ShadowCastingSetting.Off;
            mi.GIMode = GIModeEnum.Disabled;
            mi.GlobalPosition = verts[i] + (norms[i] * 0.8f);
            Vector3 up = norms[i];
            Vector3 forward = up.Cross(Vector3.Right).Normalized();
            Vector3 right = forward.Cross(up);
            mi.GlobalBasis = new Basis(right, up, forward);
            all_points[mi.GlobalPosition] = mi;
        }
    }

    public void ScanRay(Vector3 origin, Vector3 direction, float radius)
    {
        float r2 = radius * radius;
        Vector3 o2o = origin - GlobalPosition;
        foreach (KeyValuePair<Vector3, MeshInstance3D> pair in all_points)
        {
            if (o2o.Dot(pair.Key) < 0)
                continue;

            float dist = (origin - pair.Key).Cross(direction).LengthSquared();
            if (dist < r2)
            {
                all_points.Remove(pair.Key);
                RemoveChild(pair.Value);
            }
        }

        return;



        PhysicsRayQueryParameters3D param = new PhysicsRayQueryParameters3D();
        param.From = origin;
        param.To = param.From + (direction * 300.0f);
        Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(param);

        if (res.Count == 0)
            return;

        //Vector3 local_pos = GlobalBasis.Inverse() * res["position"].AsVector3();
        //Vector3 closest = Vector3.Inf;
        //Vector3 closest_normal = Vector3.Zero;
        //LinkedListNode<Vector3> normal_node = unscanned_normals.First;
        //float min_dist = float.PositiveInfinity;
        //int closest_index = 0;
        //int index = 0;
        //foreach (Vector3 v in unscanned_points)
        //{
        //    float dist = v.DistanceSquaredTo(local_pos);
        //    if (dist < min_dist)
        //    {
        //        min_dist = dist;
        //        closest = v;
        //        closest_normal = normal_node.Value;
        //        closest_index = index;
        //    }
        //    normal_node = normal_node.Next;
        //    index++;
        //}
        //if (min_dist < (radius * radius))
        //{
        //    unscanned_points.Remove(closest);
        //    unscanned_normals.Remove(closest_normal);
        //    all_points[closest] = true;
        //    GD.Print(closest + " has been scanned");
        //}
    }
}
