// Classic ground: roads, tracks, rivers and jungle floor over the grass. The colour and coverage come
// from the smoothed terrain map (GridImage); a photographed ground texture, laid in world space and
// divided by its own average, adds the grain of soil and leaf litter without changing the colour.
// Set up from C# (BattleView.Terrain.cs).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back;

uniform sampler2D terrain : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D detail : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D detail_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;

// The detail texture's average colour, so dividing by it keeps the terrain map's colour.
uniform vec3 detail_mean = vec3(0.4);

// Metres per repeat of the detail texture.
uniform float detail_size = 3.0;

varying vec2 world_xz;

void vertex() {
	world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
	vec4 ground = texture(terrain, UV);
	vec2 tile = world_xz / detail_size;
	vec3 grain = texture(detail, tile).rgb / detail_mean;
	ALBEDO = ground.rgb * grain;
	ALPHA = ground.a;
	NORMAL_MAP = texture(detail_normal, tile).rgb;
	ROUGHNESS = 0.92;
	SPECULAR = 0.25;
}
