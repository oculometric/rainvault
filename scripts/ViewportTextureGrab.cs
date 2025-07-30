using Godot;
using System;
using System.Collections.Generic;

public partial class ViewportTextureGrab : ColorRect
{
    [Export] Godot.Collections.Array<SubViewport> viewports;
    [Export] Godot.Collections.Array<string> shader_parameters;

    public override void _Process(double delta)
    {
        for (int i = 0; i < viewports.Count; i++)
        {
            if (i >= shader_parameters.Count)
                break;
            if (viewports[i] != null) (Material as ShaderMaterial).SetShaderParameter(shader_parameters[i], viewports[i].GetTexture());
        }
    }
}
