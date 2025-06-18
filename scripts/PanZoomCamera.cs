using Godot;
using System;

public partial class PanZoomCamera : Camera3D
{
    public override void _Process(double delta)
    {
        GetParent<Node3D>().RotateY((float)delta * 2.0f);
    }
}
