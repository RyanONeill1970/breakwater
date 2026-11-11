shader_type spatial;
render_mode cull_disabled, depth_draw_never;

uniform sampler2D land_field : filter_linear_mipmap, repeat_disable;
uniform sampler2D scene_depth : hint_depth_texture, filter_nearest, repeat_disable;
uniform float extent = 140.0;
uniform vec3 deep_color : source_color;
uniform vec3 shallow_color : source_color;
uniform vec3 foam_color : source_color;
varying vec3 world;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
vec4 wave_phase(vec2 p) {
    float drift = noise(p * .18) * 4.0;
    float small = noise(p * .65 + vec2(TIME * .035, 0)) * 3.0;
    return vec4(dot(p, vec2(.19, .33)), dot(p, vec2(-.47, .15)), dot(p, vec2(1.8, .75)), dot(p, vec2(-.9, 2.1)))
        + vec4(drift, drift, small, small) + TIME * vec4(.7, -.9, 1.5, -1.7);
}
float waves(vec2 p) {
    return dot(sin(wave_phase(p)), vec4(.2, .14, .038, .025));
}
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 1)).xyz;
    VERTEX.y += waves(world.xz) * .16;
    world = (MODEL_MATRIX * vec4(VERTEX, 1)).xyz;
}
void fragment() {
    float shore = (texture(land_field, (world.xz / extent + 1.0) * .5).r - .5) * 80.0;
    float offshore = max(-shore, 0.0);
    // Reconstruct the actual sea bed or rock beneath the water, including the quay wall.
    float depth_sample = textureLod(scene_depth, SCREEN_UV, 0.0).r;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(SCREEN_UV, depth_sample) * 2.0 - 1.0;
#else
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth_sample);
#endif
    vec4 beneath = INV_VIEW_MATRIX * INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    float raw_depth = world.y - beneath.y / beneath.w;
    // Resolved MSAA depth can sample a foreground leaf at a partly covered pixel. It must not make foam.
    float bed_visible = smoothstep(-.03, .06, raw_depth);
    float water_depth = mix(max(.25, offshore * .24), max(raw_depth, 0.0), bed_visible);
    float pattern = noise(world.xz * .19 + vec2(TIME * .04, 0));
    float ripple = noise(world.xz * 1.9 + vec2(TIME * .16, -TIME * .1));
    float lace = noise(world.xz * 4.7 + vec2(-TIME * .12, TIME * .17));
    vec4 slopes = cos(wave_phase(world.xz)) * vec4(.2, .14, .038, .025);
    vec2 gradient = vec2(dot(slopes, vec4(.19, -.47, 1.8, -.9)), dot(slopes, vec4(.33, .15, .75, 2.1)));
    vec3 wave_normal = normalize(vec3(-gradient.x * .5, 1, -gradient.y * .5));
    // Uneven submerged shelves read through the turquoise shallows instead of a flat color band.
    float reef = smoothstep(.48, .69, noise(world.xz * .12) + noise(world.xz * .43) * .22);
    vec3 base = mix(shallow_color, deep_color, smoothstep(.6, 6.5, water_depth + pattern));
    base *= (1.0 - reef * .3 * (1.0 - smoothstep(2.0, 8.0, water_depth))) * (.84 + .25 * pattern);

    float breaker = sin(offshore * 1.45 - TIME * 1.25 + pattern * 3.0);
    float broken_crest = smoothstep(.62, .95, breaker) * (1.0 - smoothstep(2.0, 8.0, offshore));
    float contact = (1.0 - smoothstep(.06, .35 + pattern * .35, water_depth)) * bed_visible;
    float cells = smoothstep(.25, .68, ripple * .64 + lace * .36);
    float foam = max(contact * .58, broken_crest * .44 * bed_visible) * cells;
    foam += contact * smoothstep(.55, .76, lace) * .2;
    foam = clamp(foam, 0.0, .9);
    NORMAL = normalize((VIEW_MATRIX * vec4(mix(wave_normal, vec3(0, 1, 0), foam * .75), 0)).xyz);
    ALBEDO = mix(base, foam_color, foam);
    ROUGHNESS = mix(.23, .9, foam);
    SPECULAR = .5;
    // Writing alpha schedules the water after opaque terrain, so its depth texture cannot include itself.
    ALPHA = clamp(mix(.3, 1.0, 1.0 - exp(-water_depth * .85)) + foam * .7, 0.0, 1.0);
}
