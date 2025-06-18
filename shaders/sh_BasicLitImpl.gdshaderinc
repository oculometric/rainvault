#ifdef GLASS
render_mode cull_disabled, blend_add;
#else
render_mode cull_disabled;
#endif

uniform sampler2D albedo_tex : source_color, hint_default_white, filter_nearest;

uniform bool use_triplanar = false;
uniform float triplanar_scale = 1.0f;

uniform bool randomise_uv_offset = false;
uniform float randomise_resolution = 1024.0f;
uniform bool randomise_lock_y = false;

const vec2 snapping_resolution = vec2(640.0f, 480.0f);
const float snapping_scale = 0.5f;

varying vec3 world_position;
varying vec3 world_normal;
varying vec4 unsnapped_position;

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

	unsnapped_position = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1);

	POSITION = snapXY(unsnapped_position);

	if (determinant(mat3(MODEL_MATRIX)) < 0.0f)
		NORMAL = -NORMAL;
}

float random_float(vec3 s)
{
	return fract(sin(dot(s, vec3(125.63414f, 549.325268f, 48.39574f))) * 2384.58302);
}

void fragment()
{
	vec2 uv;
	if (use_triplanar) uv = (abs(world_normal.y) < 0.707 ? (abs(world_normal.z) < 0.707 ? world_position.zy : world_position.xy) : world_position.xz) * vec2(1.0, -1.0) / triplanar_scale;
	else uv = UV;
	if (!use_triplanar && randomise_uv_offset)
	{
		uv += (floor(random_float(NODE_POSITION_WORLD) * randomise_resolution) / randomise_resolution) * vec2(1.0f, 1.0f - float(randomise_lock_y));
	}
	vec4 col = texture(albedo_tex, uv);
	ALBEDO = col.rgb;
#ifdef GLASS
	ALBEDO = vec3(1.0f);
	ALPHA = clamp(col.r * 2.0f, 0.0f, 1.0f);
#else
	ALPHA = col.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5f;
#endif

	ROUGHNESS = 1.0f;
	SPECULAR = 0.2f;
}
