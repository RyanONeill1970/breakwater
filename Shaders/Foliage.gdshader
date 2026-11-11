// Leaves: tree canopies and palm fronds. The colour comes from the mesh and the tree's instance
// colour, broken up by world-space noise into clumps and dappled leaves, darkened low in the crown
// where little light reaches, and swayed by the wind. Fronds cut their leaflets out of a plain strip.
// Built from C# (Flora.cs), which puts each vertex's height in the crown (0 low, 1 high) in UV2.y.
shader_type spatial;
render_mode cull_disabled, diffuse_lambert_wrap, specular_schlick_ggx;

// Cut leaflets out of the strip (palm fronds) rather than drawing it solid (canopies).
uniform bool leaflets = false;

// Leaflets along a frond.
uniform float leaflet_count = 24.0;

// Metres the top of a crown sways in the wind, and radians per second.
uniform float sway = 0.1;
uniform float sway_rate = 1.2;

// Metres the canopy's surface bulges in and out, and bulges per metre.
uniform float lumps = 0.0;
uniform float lump_frequency = 0.9;

varying vec3 world_position;
varying float crown;

#include "res://Shaders/Noise.gdshaderinc"

void vertex() {
	crown = UV2.y;
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (lumps > 0.0) {
		VERTEX += NORMAL * (noise(world * lump_frequency) - 0.5) * 2.0 * lumps;
	}

	// Each tree sways on its own phase, taken from where it stands.
	vec3 root = MODEL_MATRIX[3].xyz;
	float t = TIME * sway_rate + root.x * 0.37 + root.z * 0.23;
	VERTEX.x += sin(t) * sway * crown;
	VERTEX.z += cos(t * 0.8) * sway * 0.6 * crown;
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	if (leaflets) {
		// UV.x runs across the frond (0.5 on the midrib), UV.y from its base to its tip.
		float across = abs(UV.x * 2.0 - 1.0);
		// Leaflets sweep back towards the base and shorten towards the tip.
		float slot = fract(UV.y * leaflet_count + across * 1.6);
		float reach = 1.0 - UV.y * UV.y * 0.7;
		if (across > 0.07 && (slot > 0.62 || across > reach)) {
			discard;
		}
	}

	float clump = noise(world_position * 0.7);
	float leaf = noise(world_position * 5.0);
	vec3 color = COLOR.rgb * mix(0.72, 1.18, clump) * mix(0.8, 1.1, leaf);
	// Sunlit clumps yellow slightly.
	color = mix(color, color * vec3(1.1, 1.06, 0.78), smoothstep(0.62, 0.92, clump));
	ALBEDO = color * mix(0.6, 1.08, crown);
	ROUGHNESS = 0.8;
	SPECULAR = 0.25;
}
