shader_type spatial;
render_mode wireframe, unshaded;

varying vec3 world_position;

void vertex()
{
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1)).xyz;
}

void fragment()
{
	ALBEDO = clamp(world_position, vec3(0), vec3(1));
}
