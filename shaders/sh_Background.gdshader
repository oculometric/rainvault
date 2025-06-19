shader_type spatial;
render_mode unshaded, fog_disabled;

#include "sh_Voronoi.gdshaderinc"

uniform sampler2D depth_texture : hint_depth_texture;

void vertex()
{
	POSITION = vec4(VERTEX.xy, 1.0f, 1.0f);
}

vec3 star_func(vec3 co, float r)
{
	vec3 v;
	float f = voronoi(co, r, v);
	float b = pow(1.0f - f, 100.0f) * 50.0f;
	float c = voronoi_hash(v);
	float cr = (1.0f - (c * 0.8f)) + 0.3f;
	float cg = 0.6f;
	float c_ = (1.4f * c) + 0.25f;
	float cb = ((c_ * c_ * (3.0f - (2.0f * c_))) * 0.8f) + 0.1f;
	return clamp(vec3(cr, cg, cb), vec3(0.0f), vec3(1.0f)) * b;
}

void fragment()
{
	float depth = texture(depth_texture, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV * 2.0f - 1.0f, depth);
	vec4 world = INV_VIEW_MATRIX * ((INV_PROJECTION_MATRIX * vec4(ndc, 1.0f)) * vec4(1.0f, 1.0f, 1.0f, 0.0f));
	vec3 dir = normalize(world.xyz);

	ALBEDO = star_func(dir * 10.0f, 1.0f) + star_func(dir * 25.0f, 0.6f) + star_func(dir * 55.0f, 0.6f);
	DEPTH = 0.0f;
}
