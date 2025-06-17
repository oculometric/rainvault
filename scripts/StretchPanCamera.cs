using Godot;
using System;

public partial class StretchPanCamera : Camera3D
{
    public override void _Ready()
    {
        Input.WarpMouse(GetWindow().Size / 2);
    }

    private float MouseFunction(float f)
    {
        return Mathf.Sin(Mathf.Min(Mathf.Abs(f), 1.0f) * Mathf.Pi / 2.0f) * Mathf.Sign(f);
    }

    public override void _Process(double delta)
    {
        Vector2 mouse_pos = GetViewport().GetMousePosition() / GetViewport().GetVisibleRect().Size;
        Vector2 mouse_relative = (mouse_pos * 2.0f) - new Vector2(1.0f, 1.0f);
        mouse_relative.X = MouseFunction(mouse_relative.X);
        mouse_relative.Y = MouseFunction(mouse_relative.Y);

        RotationDegrees = new Vector3(mouse_relative.Y * -8.0f, mouse_relative.X * -8.0f, 0.0f);
	}
}
