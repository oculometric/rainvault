using Godot;
using System;

public partial class TrackedCamera : Camera3D
{
    [Export] private Camera3D target;

    public override void _Process(double delta)
    {
        GlobalTransform = target.GlobalTransform;
    }
}
