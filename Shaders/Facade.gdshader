// A building's walls and flat roof, drawn on one box. The walls are divided into storeys and bays,
// each bay with a framed window: glass that reflects the sky at a glancing angle, some windows
// shuttered, and at night some lit. Rendered walls fade and streak with grime below the windows
// and darken at the foot; the roof is weathered concrete. Set up from C# (BattleView.Scenery.cs).
shader_type spatial;
render_mode specular_schlick_ggx;

#include "res://Shaders/Noise.gdshaderinc"

uniform vec3 wall_color : source_color;
uniform vec3 trim_color : source_color;
uniform vec3 roof_color : source_color;
uniform vec3 glass_color : source_color;
uniform vec3 sky_color : source_color;
uniform vec3 shutter_color : source_color;
uniform vec3 lamp_color : source_color;

// Photographed render and concrete, and their average colours: dividing by the average leaves
// just their grain, so the paint colours above still decide the colour.
uniform sampler2D wall_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D roof_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 wall_mean = vec3(0.5);
uniform vec3 roof_mean = vec3(0.5);

// Metres per repeat of the wall and roof textures.
uniform float texture_size = 2.5;

// The footprint's centre at ground level, its half width and depth, and the wall height, in metres.
uniform vec3 center;
uniform vec2 half_size;
uniform float height = 5.0;

// Storey height, and the widest a bay may be, in metres.
uniform float storey = 2.4;
uniform float bay = 3.0;

// Fraction of windows shuttered, and at night the fraction lit.
uniform float shuttered = 0.2;
uniform float lit = 0.0;

// Varies the grime and the shutters from building to building.
uniform float seed = 0.0;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 local = world_position - center;
	float grain = noise(world_position * 6.0 + seed);
	if (world_normal.y > 0.5) {
		// The roof: concrete with patches and fine grain.
		vec3 slab = texture(roof_texture, world_position.xz / texture_size).rgb / roof_mean;
		ALBEDO = roof_color * slab * (0.85 + 0.25 * noise(world_position * 0.9 + seed));
		ROUGHNESS = 0.95;
	} else {
		// Position along the wall from its left end, the wall's length, and height above the ground.
		bool across_x = abs(world_normal.x) > abs(world_normal.z);
		float u = across_x ? local.z + half_size.y : local.x + half_size.x;
		float wall_length = across_x ? half_size.y * 2.0 : half_size.x * 2.0;
		float v = local.y;
		float bays = max(1.0, floor(wall_length / bay));
		float bay_width = wall_length / bays;
		vec2 cell = vec2(u / bay_width, v / storey);
		vec2 id = floor(cell);
		vec2 f = fract(cell);
		float window_id = hash(vec3(id, seed + (across_x ? 7.0 : 3.0) * sign(across_x ? world_normal.x : world_normal.z)));

		vec3 render = texture(wall_texture, vec2(u, v) / texture_size).rgb / wall_mean;
		vec3 color = wall_color * render * (0.88 + 0.22 * noise(world_position * vec3(0.6, 0.3, 0.6) + seed));
		// Rain streaks run down from the sills; dirt splashes up from the ground.
		bool under_window = f.x > 0.3 && f.x < 0.7 && f.y < 0.32;
		float streak = noise(vec3(u * 3.0, v * 0.4, seed));
		if (under_window) {
			color *= mix(1.0, 0.8 + 0.1 * streak, 1.0 - f.y / 0.32);
		}
		color *= mix(0.62, 1.0, smoothstep(0.0, 1.4, v));

		// A trim band at each floor and at the corners.
		bool band = fract(v / storey) < 0.05 && v > storey * 0.5;
		bool corner = u < 0.22 || u > wall_length - 0.22;
		if (band || corner) {
			color = trim_color * (0.9 + 0.15 * grain);
		}

		ROUGHNESS = 0.92;
		SPECULAR = 0.3;
		// A window in each bay of each full storey.
		// The cornice hides the last few centimetres of wall, so a storey that nearly fits still gets windows.
		bool full_storey = (id.y + 1.0) * storey <= height - 0.1;
		vec2 low = vec2(0.28, 0.34);
		vec2 high = vec2(0.72, 0.82);
		if (!corner && full_storey && all(greaterThan(f, low)) && all(lessThan(f, high))) {
			vec2 in_window = (f - low) / (high - low);
			float frame = min(min(in_window.x, 1.0 - in_window.x), min(in_window.y, 1.0 - in_window.y));
			if (frame < 0.07) {
				color = trim_color * 0.85;
			} else if (window_id < shuttered) {
				// Closed louvred shutters.
				color = shutter_color * (0.8 + 0.25 * step(0.5, fract(in_window.y * 9.0)));
			} else {
				// Glass: dark inside, the sky reflected more strongly at a glancing angle, and a mullion.
				float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
				color = mix(glass_color, sky_color, clamp(0.12 + fresnel * 0.9 + in_window.y * 0.15, 0.0, 1.0));
				if (abs(in_window.x - 0.5) < 0.035) {
					color = trim_color * 0.8;
				}
				ROUGHNESS = 0.08;
				SPECULAR = 0.7;
				if (window_id > 1.0 - lit) {
					EMISSION = lamp_color * (0.7 + 0.5 * hash(vec3(id * 3.1, seed)));
				}
			}
		}

		ALBEDO = color;
	}
}
