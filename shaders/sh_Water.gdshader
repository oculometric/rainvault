shader_type spatial;
//render_mode ;

uniform sampler2D depth_texture : hint_depth_texture;
uniform vec3 light_blue : source_color;
uniform vec3 dark_blue : source_color;

void vertex()
{
}

void fragment()
{
	float depth = texture(depth_texture, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV * 2.0f - 1.0f, depth);
	vec4 view = (INV_PROJECTION_MATRIX * vec4(ndc, 1.0f)) * vec4(1.0f, 1.0f, 1.0f, 1.0f);
	vec4 world = INV_VIEW_MATRIX * view;
	vec4 local = inverse(MODEL_MATRIX) * world;
	local /= local.w;
	vec3 dir = normalize(world.xyz);
	
	float falloff = pow(smoothstep(0.0f, 1.0f, -local.y), 0.4f);
	
	ALBEDO = mix(light_blue, dark_blue, clamp(falloff * 2.0f, 0.0f, 1.0f));//vec3(falloff);
	ALPHA = clamp((falloff * 1.8f) + 0.3f, 0, 1);//(dot(dir, vec3(0, -1, 0)));
}