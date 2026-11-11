// Rocky ridges: photographed rock laid on from all three sides (triplanar, so the steep faces
// don't smear), with photographed grass on the flatter ground along the top, joined along a
// ragged edge. Drawn on the ridge's heightfield (BattleView.Scenery.cs).
shader_type spatial;
render_mode cull_disabled, specular_schlick_ggx;

#include "res://Shaders/Noise.gdshaderinc"

uniform sampler2D rock_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;

// Multipliers that bring the photographs to the game's rock and grass colours.
uniform vec3 rock_tint = vec3(1.0);
uniform vec3 grass_tint = vec3(1.0);

// Metres per repeat of the rock and grass textures.
uniform float rock_size = 4.0;
uniform float grass_size = 4.0;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// A texture sampled along each axis and blended by how squarely the surface faces it.
vec3 triplanar(sampler2D map, vec3 p, vec3 weights) {
	return texture(map, p.zy).rgb * weights.x + texture(map, p.xz).rgb * weights.y + texture(map, p.xy).rgb * weights.z;
}

void fragment() {
	vec3 weights = pow(abs(world_normal), vec3(4.0));
	weights /= weights.x + weights.y + weights.z;
	vec3 rock = triplanar(rock_albedo, world_position / rock_size, weights);

	// Grass holds where the ground is flat enough, with a ragged edge.
	float flat_ground = world_normal.y + (noise(world_position * 0.6) - 0.5) * 0.35;
	float grass = smoothstep(0.72, 0.88, flat_ground);
	vec3 turf = texture(grass_albedo, world_position.xz / grass_size).rgb;
	ALBEDO = mix(rock * rock_tint, turf * grass_tint, grass);
	// The rock's relief, taken from the side it's seen from most squarely.
	vec2 face = weights.x > weights.z ? world_position.zy : world_position.xy;
	NORMAL_MAP = mix(texture(rock_normal, face / rock_size).rgb, vec3(0.5, 0.5, 1.0), max(grass, weights.y));
	ROUGHNESS = 0.92;
	SPECULAR = 0.25;
}
