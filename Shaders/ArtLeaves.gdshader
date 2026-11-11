shader_type spatial;
render_mode cull_disabled;

uniform sampler2D leaf_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 leaf_color : source_color;

void fragment() {
    vec3 detail = texture(leaf_tex, UV).rgb;
    float value = max(detail.r, max(detail.g, detail.b));
    ALBEDO = mix(leaf_color, detail, .45);
    ALPHA = smoothstep(.006, .025, value);
    ALPHA_SCISSOR_THRESHOLD = .5;
    ROUGHNESS = .9;
    SPECULAR = .08;
    BACKLIGHT = leaf_color * .4;
}
