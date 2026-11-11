// The open sea around the island: rolling swell, colour that deepens away from the beach, a
// breathing foam line along the sand, and a glint of ripples. Colours and the beach outline are
// set from C# (BattleView.Environment.cs) so they stay in Palette.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform vec3 deep_color : source_color;
uniform vec3 shallow_color : source_color;
uniform vec3 foam_color : source_color;

// Half the width and depth of the beach, in metres: the shoreline the foam follows.
uniform vec2 shore_half_extent = vec2(100.0, 100.0);

// Metres of foam at the waterline, and metres of open water over which shallows deepen to blue.
uniform float foam_width = 5.0;
uniform float shelf_width = 140.0;

// Peak swell height, in metres.
uniform float wave_height = 0.3;

varying vec3 world_position;

// A few directional sine waves (wave vector in radians per metre, speed, weight): cheap and tileless.
float swell(vec2 p, float t) {
	float h = sin(dot(p, vec2(0.12, 0.05)) + t * 1.1) * 0.5;
	h += sin(dot(p, vec2(-0.07, 0.11)) + t * 1.4) * 0.3;
	h += sin(dot(p, vec2(0.21, -0.17)) + t * 2.1) * 0.2;
	return h * wave_height;
}

// Signed distance from the beach rectangle, positive out to sea.
float shore_distance(vec2 p) {
	vec2 d = abs(p) - shore_half_extent;
	return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.y += swell(world_position.xz, TIME);

	// The normal from the swell's slope, by central differences half a metre apart.
	const float e = 0.5;
	float dx = swell(world_position.xz + vec2(e, 0.0), TIME) - swell(world_position.xz - vec2(e, 0.0), TIME);
	float dz = swell(world_position.xz + vec2(0.0, e), TIME) - swell(world_position.xz - vec2(0.0, e), TIME);
	NORMAL = normalize(vec3(-dx / (2.0 * e), 1.0, -dz / (2.0 * e)));
}

void fragment() {
	float shore = shore_distance(world_position.xz);
	vec3 color = mix(shallow_color, deep_color, smoothstep(0.0, shelf_width, shore));

	// Small crossing ripples catch the light.
	float ripple = sin(world_position.x * 0.9 + TIME * 2.0) * sin(world_position.z * 0.8 - TIME * 1.7);
	color += smoothstep(0.75, 1.0, ripple) * 0.05;

	// Foam washes up and back along the beach, unevenly along its length.
	float wash = sin(TIME * 1.3 + world_position.x * 0.17 + world_position.z * 0.13) * 1.6;
	float foam = 1.0 - smoothstep(0.0, foam_width, shore + wash);
	color = mix(color, foam_color, foam * 0.85);

	ALBEDO = color;
	ROUGHNESS = 0.3;
	SPECULAR = 0.45;
}
