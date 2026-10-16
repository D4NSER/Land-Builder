shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 deep_color : source_color = vec4(0.180, 0.545, 0.659, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.320, 0.690, 0.760, 1.0);
uniform vec4 rim_color : source_color = vec4(0.920, 0.980, 1.000, 1.0);
uniform float wave_scale = 2.2;
uniform float wave_height = 0.04;
uniform float wave_speed = 0.40;
uniform float fresnel_power = 4.4;
uniform float fresnel_strength = 0.45;
uniform float metallic_amount = 0.10;
uniform float roughness_amount = 0.08;

float wave_layer(vec2 p, vec2 dir, float speed, float frequency)
{
	return sin(dot(p, dir) * frequency + TIME * speed);
}

void vertex()
{
	vec2 p = VERTEX.xz * wave_scale;
	float wave_a = wave_layer(p, normalize(vec2(1.0, 0.4)), wave_speed * 4.2, 1.2);
	float wave_b = wave_layer(p, normalize(vec2(-0.6, 1.0)), wave_speed * 3.0, 1.7);
	float wave_c = wave_layer(p, normalize(vec2(0.2, -1.0)), wave_speed * 5.0, 2.4);
	VERTEX.y += (wave_a * 0.45 + wave_b * 0.35 + wave_c * 0.20) * wave_height;
}

void fragment()
{
	vec2 p = UV * wave_scale * 8.0;
	float wave_a = wave_layer(p, normalize(vec2(1.0, 0.3)), wave_speed * 4.5, 1.5);
	float wave_b = wave_layer(p, normalize(vec2(-0.7, 1.0)), wave_speed * 3.3, 2.1);
	float wave_c = wave_layer(p, normalize(vec2(0.4, -1.0)), wave_speed * 5.4, 2.9);
	float mix_value = clamp(0.5 + wave_a * 0.18 + wave_b * 0.16 + wave_c * 0.12, 0.0, 1.0);

	vec3 tangent_normal = normalize(vec3(wave_a * 0.28 + wave_c * 0.18, 1.0, wave_b * 0.28 - wave_c * 0.12));
	NORMAL = tangent_normal;

	float fresnel = pow(1.0 - max(dot(normalize(NORMAL), normalize(VIEW)), 0.0), fresnel_power) * fresnel_strength;
	float sparkle = smoothstep(0.78, 0.97, mix_value + fresnel * 0.35);
	vec3 water = mix(deep_color.rgb * 0.82, shallow_color.rgb, mix_value);
	ALBEDO = mix(water, rim_color.rgb, fresnel * 0.65 + sparkle * 0.18);
	EMISSION = rim_color.rgb * (fresnel * 0.16 + sparkle * 0.10);
	ALPHA = 1.0;
	METALLIC = metallic_amount;
	ROUGHNESS = roughness_amount + (1.0 - mix_value) * 0.03;
	SPECULAR = 0.95;
}
