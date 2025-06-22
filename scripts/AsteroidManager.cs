using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class AsteroidManager : MeshInstance3D
{
    [Export] private CpuParticles3D particles;

    private Dictionary<Vector3, bool> all_points = new Dictionary<Vector3, bool>(); // TODO: convert this to a pair of arrays so we dont have to iterate a lot
    private List<Vector3> unscanned_points = new List<Vector3>();

    public override void _Ready()
    {
        ArrayMesh am = Mesh as ArrayMesh;
        foreach (Vector3 v in am.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
            all_points[v] = false;

        unscanned_points = all_points.Keys.ToList();

        particles.EmissionPoints = unscanned_points.ToArray();
    }

    public override void _Process(double delta)
    {
        PhysicsRayQueryParameters3D param = new PhysicsRayQueryParameters3D();
        param.From = GetViewport().GetCamera3D().GlobalPosition;
        param.To = param.From + (GetViewport().GetCamera3D().ProjectRayNormal(GetViewport().GetMousePosition()) * 100.0f);
        Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(param);

        if (res.Count == 0)
            return;

        Vector3 local_pos = GlobalBasis.Inverse() * res["position"].AsVector3();

        Vector3 closest = Vector3.Inf;
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
                closest_index = index;
            }
            index++;
        }
        if (Mathf.Sqrt(min_dist) < 1.0f / GlobalBasis.Scale.X)
        {
            unscanned_points.RemoveAt(closest_index);
            all_points[closest] = true;
            GD.Print(closest + " has been scanned");
            particles.EmissionPoints = unscanned_points.ToArray();
        }
    }
}
