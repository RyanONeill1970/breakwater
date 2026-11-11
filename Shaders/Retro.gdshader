// The retro look: the 3D view redrawn at a low resolution with square pixels, a small palette and
// an ordered dither, in the manner of early-90s home computers. Drawn on a full-screen rect under
// the HUD, so the interface stays sharp. The resolution is set from C# to match the window's shape.
shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_nearest;

// Pixels across and down the retro screen.
uniform vec2 resolution = vec2(320.0, 200.0);

// Brightness steps per colour channel: 6 gives 216 colours.
uniform float levels = 6.0;

// How far the dither may push a pixel towards the next step, as a fraction of one step.
uniform float dither = 0.5;

void fragment() {
	vec2 cell = floor(SCREEN_UV * resolution);
	vec3 color = texture(screen, (cell + 0.5) / resolution).rgb;
	// A 2 x 2 ordered (Bayer) threshold, centred on zero.
	float bayer = (mod(cell.x, 2.0) * 2.0 + mod(cell.y, 2.0) * 3.0 - mod(cell.x, 2.0) * mod(cell.y, 2.0) * 4.0) / 4.0 - 0.375;
	float steps = levels - 1.0;
	COLOR = vec4(floor(color * steps + 0.5 + bayer * dither) / steps, 1.0);
}
