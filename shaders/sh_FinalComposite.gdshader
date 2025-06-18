shader_type canvas_item;

uniform sampler2D ui_texture : source_color, hint_default_transparent, repeat_disable, filter_nearest;

void fragment()
{
	COLOR = texture(ui_texture, UV) * vec4(1.0f, 1.0f, 1.0f, 0.5f);//vec4(1.0f, 0.0f, 0.0f, 1.0f);
}