shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

// 0 = collapsed, 1 = fully grown. Props pop in one by one using the delay stored in CUSTOM0.w.
instance uniform float grow = 1.0;
instance uniform vec4 highlight : source_color = vec4(1.0, 1.0, 1.0, 0.0);
instance uniform float dim = 0.0;

uniform float wind_strength = 1.0;

float ease_out_back(float t) {
	float c1 = 1.9;
	float c3 = c1 + 1.0;
	float u = t - 1.0;
	return 1.0 + c3 * u * u * u + c1 * u * u;
}

void vertex() {
	vec3 pivot = CUSTOM0.xyz;
	float delay = CUSTOM0.w;
	float t = clamp((grow - delay * 0.6) / 0.4, 0.0, 1.0);
	float s = max(ease_out_back(t), 0.0);
	vec3 local = (VERTEX - pivot) * s;

	float sway = COLOR.a;
	vec3 wp = (MODEL_MATRIX * vec4(pivot, 1.0)).xyz;
	float phase = TIME * 1.7 + wp.x * 1.3 + wp.z * 0.9;
	float gust = 0.6 + 0.4 * sin(TIME * 0.35 + wp.x * 0.2);
	float h = max(local.y, 0.0);
	local.x += sin(phase) * 0.05 * wind_strength * sway * h * gust;
	local.z += cos(phase * 0.83 + 1.0) * 0.035 * wind_strength * sway * h * gust;

	VERTEX = pivot + local;
}

void fragment() {
	vec3 c = srgb_to_linear(COLOR.rgb);
	vec3 hl = srgb_to_linear(highlight.rgb);
	c = mix(c, hl, highlight.a * 0.3);
	ALBEDO = c * (1.0 - dim);
	ROUGHNESS = 0.85;
	SPECULAR = 0.3;
	EMISSION = hl * highlight.a * 0.18;
}
