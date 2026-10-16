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

	// Seasons: greens freshen in spring, warm up in autumn and dry out on arid islands; snow settles on flat ground.
	float green = clamp((base.g - max(base.r, base.b)) * 6.0, 0.0, 1.0);
	c = mix(c, c * vec3(0.95, 1.12, 0.9), green * season_spring * 0.35);
	c = mix(c, c * vec3(1.3, 0.95, 0.5), green * (season_autumn * 0.45 + season_dry * 0.75));
	float flat_ground = smoothstep(0.55, 0.9, world_normal.y);
	float drift = lb_noise(world_pos.xz * 2.3) * 0.6 + lb_noise(world_pos.xz * 7.1) * 0.4;
	float snow = season_snow * flat_ground * smoothstep(0.78 - season_snow * 0.45, 0.98 - season_snow * 0.4, drift);
	c = mix(c, vec3(0.86, 0.9, 0.96), snow);

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
