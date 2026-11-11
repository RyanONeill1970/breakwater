shader_type spatial;
render_mode cull_disabled;

uniform sampler2D land_field : filter_linear_mipmap, repeat_disable;
uniform sampler2D sand_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D asphalt_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D sand_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 rock_color : source_color;
uniform vec3 sand_color : source_color;
uniform vec3 grass_color : source_color;
uniform vec3 earth_color : source_color;
uniform vec3 road_color : source_color;
uniform float extent = 140.0;
uniform float road_half_width = 3.2;
varying vec3 world;
varying vec3 world_normal;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
    vec3 field = texture(land_field, (world.xz / extent + 1.0) * .5).rgb;
    float shore = (field.r - .5) * 80.0;
    float road = field.g * 24.0;
    float macro = noise(world.xz * .095);
    float fine = noise(world.xz * .75);
    float grass = smoothstep(5.0, 10.0, shore + macro * 5.0);
    float worn = 1.0 - smoothstep(road_half_width - .35, road_half_width + 1.4, road + (fine - .5) * .5);
    vec3 sand_detail = texture(sand_tex, world.xz * .14).rgb;
    vec3 grass_detail = texture(grass_tex, world.xz * .11).rgb;
    vec3 soil_detail = texture(soil_tex, world.xz * .16).rgb;
    vec3 asphalt_detail = texture(asphalt_tex, world.xz * .16).rgb;
    vec3 sand = sand_color * (.83 + .3 * sand_detail);
    vec3 turf = mix(earth_color, grass_color, smoothstep(.18, .67, macro)) * (.8 + .36 * grass_detail);
    vec3 soil = earth_color * (.85 + .24 * soil_detail);
    vec3 road_surface = mix(road_color, earth_color, smoothstep(-5.0, 25.0, world.x)) * (.7 + .55 * asphalt_detail);
    vec3 base = mix(sand, turf, grass);
    base = mix(base, soil, (1.0 - smoothstep(road_half_width, road_half_width + 2.8, road)) * grass * .65);
    base = mix(base, road_surface, worn * smoothstep(2.0, 7.0, shore));
    float stone_detail = dot(texture(rock_tex, world.xz * .16).rgb, vec3(.3, .59, .11));
    vec3 rock = rock_color * (.48 + stone_detail * 3.8);
    // Exposed slopes are stone; flatter pockets between outcrops keep their soil and vegetation.
    float steep = 1.0 - abs(normalize(world_normal).y);
    base = mix(base, rock, smoothstep(.12, .48, steep) * smoothstep(5.0, 13.0, world.y));
    base *= mix(.64, 1.0, smoothstep(-1.0, 3.3, shore));
    ALBEDO = base;
    NORMAL_MAP = mix(texture(sand_normal, world.xz * .14).rgb, texture(grass_normal, world.xz * .11).rgb, grass * (1.0 - worn));
    NORMAL_MAP_DEPTH = .35;
    ROUGHNESS = .92;
    SPECULAR = .12;
}
