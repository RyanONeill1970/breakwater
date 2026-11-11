shader_type spatial;
render_mode cull_disabled;
uniform sampler2D color_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D orm_tex : filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 stone_tint : source_color;
uniform vec3 moss_color : source_color;
uniform float sea_level = -0.06;
varying vec3 world;
varying vec3 world_normal;
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
    vec3 photographed = texture(color_tex, UV).rgb;
    float value = dot(photographed, vec3(.3, .59, .11));
    vec3 orm = texture(orm_tex, UV).rgb;
    // Keep the scan's crevices, but match its exposure to the island's existing stone.
    ALBEDO = (mix(photographed, vec3(value), .5) * 1.65 + .025) * stone_tint;
    float patches = sin(world.x * .7 + sin(world.z * .38)) * sin(world.z * .61 + world.y * .42) * .5 + .5;
    float moss = smoothstep(.55, .86, world_normal.y) * smoothstep(.45, .85, patches) * .45;
    ALBEDO = mix(ALBEDO, moss_color * (.5 + value), moss);
    float wet = 1.0 - smoothstep(.16, 1.41 + patches * .35, world.y - sea_level);
    ALBEDO *= 1.0 - wet * .46;
    NORMAL_MAP = texture(normal_tex, UV).rgb;
    NORMAL_MAP_DEPTH = .7;
    AO = mix(.35, 1.0, orm.r);
    AO_LIGHT_AFFECT = .2;
    ROUGHNESS = mix(max(.75, orm.g), .46, wet);
    SPECULAR = .12;
}
