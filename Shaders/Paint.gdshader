// Vehicle paint: the colour comes from the mesh (MeshBuilder), mottled so it isn't flat, with mud
// splashed up the lower hull and dust settled on upward faces. Vertex alpha says how much dirt a
// part takes: 1 for painted armour, less for metal and rubber, 0 for glass. At night the hull glows
// faintly with engine heat, which the thermal sight picks out. Set up from C# (VehicleModel.cs).
shader_type spatial;
render_mode cull_disabled, specular_schlick_ggx;

#include "res://Shaders/Noise.gdshaderinc"

uniform vec3 mud_color : source_color;

// How brightly the paint glows at night, as a multiple of its colour.
uniform float heat = 0.0;

// Mud reaches this high up the hull, in metres, with a ragged edge.
uniform float mud_line = 0.9;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float dirt = COLOR.a;
	vec3 base = COLOR.rgb * (0.9 + 0.18 * noise(world_position * 2.5)) * (0.96 + 0.08 * noise(world_position * 11.0));
	float ragged = (noise(world_position * 3.0) - 0.5) * 0.5;
	float mud = (1.0 - smoothstep(mud_line * 0.3, mud_line, world_position.y + ragged)) * dirt;
	base = mix(base, mud_color, mud * 0.7);
	float dust = smoothstep(0.6, 1.0, world_normal.y) * 0.18 * dirt;
	base = mix(base, mud_color * 1.35, dust);
	ALBEDO = base;
	ROUGHNESS = mix(0.35, 0.78, dirt);
	SPECULAR = mix(0.7, 0.35, dirt);
	EMISSION = base * heat;
}
