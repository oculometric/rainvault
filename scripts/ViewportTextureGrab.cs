using Godot;
using System;

public partial class ViewportTextureGrab : ColorRect
{
    [Export] SubViewport target_viewport;

    public override void _Process(double delta)
    {
        (Material as ShaderMaterial).SetShaderParameter("ui_texture", target_viewport.GetTexture());
    }
}
