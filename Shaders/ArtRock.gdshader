shader_type spatial;
render_mode cull_disabled;

uniform sampler2D rock_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 stone_color : source_color;
uniform vec3 moss_color : source_color;
varying vec3 world;
varying vec3 world_normal;

void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}
void fragment() {
    vec3 weight = pow(abs(world_normal), vec3(4.0));
    weight /= max(dot(weight, vec3(1.0)), .001);
    vec3 detail = texture(rock_tex, world.zy * .16).rgb * weight.x
        + texture(rock_tex, world.xz * .16).rgb * weight.y
        + texture(rock_tex, world.xy * .16).rgb * weight.z;
    float grain = dot(detail, vec3(.3, .59, .11));
    float mottling = sin(world.x * .51 + sin(world.z * .37)) * sin(world.y * .67 + world.z * .2) * .5 + .5;
    float moss = smoothstep(.48, .8, world_normal.y) * smoothstep(.4, .75, mottling) * .65;
    ALBEDO = mix(stone_color * (.48 + grain * 3.8), moss_color * (.6 + grain * 2.0), moss);
    ROUGHNESS = .96;
    SPECULAR = .1;
}
