shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

// Per-instance controls let every tile share one material.
instance uniform vec4 highlight : source_color = vec4(1.0, 1.0, 1.0, 0.0);
instance uniform float dim = 0.0;

uniform float detail_strength = 0.07;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 base = srgb_to_linear(COLOR.rgb);
	float n = lb_noise(world_pos.xz * 24.0) * 0.6 + lb_noise(world_pos.xz * 61.0) * 0.4;
	vec3 c = base * (1.0 + (n - 0.5) * detail_strength * 2.0);

	// Slightly cooler, darker steep faces read as cliffs without extra geometry.
	float steep = 1.0 - clamp(world_normal.y, 0.0, 1.0);
	c *= 1.0 - steep * 0.12;

	vec3 hl = srgb_to_linear(highlight.rgb);
	c = mix(c, hl, highlight.a * 0.3);
	ALBEDO = c * (1.0 - dim);
	ROUGHNESS = 0.93;
	SPECULAR = 0.25;
	EMISSION = hl * highlight.a * 0.18;
}
