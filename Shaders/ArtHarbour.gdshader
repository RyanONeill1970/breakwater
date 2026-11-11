shader_type spatial;
uniform sampler2D grain_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 base_color : source_color;
uniform vec3 grime_color : source_color;
uniform float grain_scale = .2;
uniform float sea_level = -0.06;
varying vec3 world;
varying vec3 face;
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    face = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
    vec3 w = pow(abs(face), vec3(4));
    w /= dot(w, vec3(1));
    vec3 grain = texture(grain_tex, world.zy * grain_scale).rgb * w.x
        + texture(grain_tex, world.xz * grain_scale).rgb * w.y
        + texture(grain_tex, world.xy * grain_scale).rgb * w.z;
    float value = dot(grain, vec3(.3, .59, .11));
    float streak = texture(grain_tex, vec2((world.x + world.z) * .8, world.y * .045)).r;
    float wet = 1.0 - smoothstep(.21, 1.56 + streak, world.y - sea_level);
    ALBEDO = mix(base_color * (.48 + 1.1 * value), grime_color, wet * .75);
    ROUGHNESS = mix(.88, .55, wet);
    SPECULAR = .16;
}
