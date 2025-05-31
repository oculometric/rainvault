shader_type spatial;
render_mode cull_disabled;

uniform sampler2D albedo_tex : source_color, hint_default_white, filter_nearest;

uniform bool use_triplanar = false;
uniform float triplanar_scale = 1.0f;
const vec2 snapping_resolution = vec2(640.0f, 480.0f);
const float snapping_scale = 1.0f;

varying vec3 world_position;
varying vec3 world_normal;

vec4 snapXY(vec4 projected_position)
{
	vec3 snapped_position = projected_position.xyz / projected_position.w;
	snapped_position.xy = round(snapped_position.xy * snapping_resolution * snapping_scale) / (snapping_resolution * snapping_scale);
	snapped_position *= projected_position.w;

	return vec4(snapped_position, projected_position.w);
}

void vertex()
{
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1)).xyz;
	world_normal = normalize((MODEL_NORMAL_MATRIX * NORMAL).xyz);

	vec4 projected_position = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1);
	projected_position = snapXY(projected_position);

	POSITION = projected_position;
}

void fragment()
{
	vec2 uv;
	if (use_triplanar) uv = (abs(world_normal.y) < 0.707 ? (abs(world_normal.z) < 0.707 ? world_position.zy : world_position.xy) : world_position.xz) * vec2(1.0, -1.0) / triplanar_scale;
	else uv = UV;
	vec4 col = texture(albedo_tex, uv);
	ALBEDO = col.rgb;
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5f;
}
