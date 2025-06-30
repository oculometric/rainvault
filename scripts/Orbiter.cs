using Godot;
using System;

public partial class Orbiter : Node3D
{
    [Export] public AsteroidManager asteroid;
    [Export] public Node3D rotator_disk;
    [Export] public Node3D orbiter_mesh;

    float orbit_speed = 1.0f;

    public override void _Process(double delta)
    {
        rotator_disk.Rotate(rotator_disk.GlobalBasis.X, (float)delta * orbit_speed * 0.25f);

        if (Input.IsKeyPressed(Key.A))
            rotator_disk.Rotate(rotator_disk.GlobalBasis.Y, (float)delta * -orbit_speed * 0.1f);
        if (Input.IsKeyPressed(Key.D))
            rotator_disk.Rotate(rotator_disk.GlobalBasis.Y, (float)delta * orbit_speed * 0.1f);
        if (Input.IsKeyPressed(Key.W))
            orbit_speed += 0.1f * (float)delta;
        if (Input.IsKeyPressed(Key.S))
            orbit_speed -= 0.1f * (float)delta;

        orbit_speed = Mathf.Clamp(orbit_speed, 0.1f, 2.5f);

        asteroid.ScanRay(orbiter_mesh.GlobalPosition, orbiter_mesh.GlobalBasis.Z, 4.0f);//GetViewport().GetCamera3D().GlobalPosition, GetViewport().GetCamera3D().ProjectRayNormal(GetViewport().GetMousePosition()), 2.0f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey)
        {
            InputEventKey iek = @event as InputEventKey;
            if (iek.IsPressed())
            {
            }
        }
    }
}
