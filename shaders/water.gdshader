shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;

uniform vec3 shallow_color : source_color = vec3(0.45, 0.82, 0.80);
uniform vec3 deep_color : source_color = vec3(0.10, 0.40, 0.60);
uniform vec3 foam_color : source_color = vec3(1.0, 1.0, 1.0);
uniform float depth_scale = 1.5;
uniform float foam_width = 0.08;
uniform float min_alpha = 0.55;
uniform float wave_height = 0.0;
uniform float ripple_scale = 3.0;
uniform float ripple_strength = 0.35;
uniform float far_fade = 0.0;
uniform vec3 murk_color : source_color = vec3(0.36, 0.42, 0.24);
uniform bool can_freeze = false;

varying float murk;

varying vec3 world_pos;

float waves(vec2 p, float t) {
	return sin(p.x * 0.9 + t * 0.9) * 0.5
		+ sin(p.y * 1.3 - t * 0.7) * 0.35
		+ sin((p.x + p.y) * 2.1 + t * 1.3) * 0.15;
}

// Sum of directional sines with analytic gradient: smooth, alias-free ripples.
vec2 ripple_gradient(vec2 p, float t) {
	vec2 g = vec2(0.0);
	vec2 dirs[5] = vec2[](vec2(0.83, 0.55), vec2(-0.45, 0.89), vec2(0.97, -0.24), vec2(-0.71, -0.70), vec2(0.20, 0.98));
	float freqs[5] = float[](1.0, 1.37, 1.91, 2.63, 3.4);
	float speeds[5] = float[](0.9, 1.1, 1.3, 1.6, 2.0);
	float amps[5] = float[](1.0, 0.7, 0.45, 0.3, 0.2);
	for (int i = 0; i < 5; i++) {
		float f = freqs[i] * ripple_scale;
		float phase = dot(p, dirs[i]) * f + t * speeds[i] + float(i) * 1.7;
		g += dirs[i] * cos(phase) * f * amps[i];
	}
	// Slow large-scale variation so the pattern doesn't read as a grid.
	float warp = lb_noise(p * 0.15 + vec2(t * 0.02, 0.0));
	return g * (0.55 + warp * 0.6) / ripple_scale;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Meshes without vertex colours read as white, i.e. perfectly clear water.
	murk = 1.0 - COLOR.r;
	if (wave_height > 0.0) {
		VERTEX.y += waves(world_pos.xz, TIME) * wave_height;
		world_pos.y += waves(world_pos.xz, TIME) * wave_height;
	}
}

void fragment() {
	float depth = texture(depth_tex, SCREEN_UV).r;
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	float thickness = max(0.0, length(view.xyz) - length(VERTEX));
	// Convert the slanted thickness into roughly vertical depth.
	vec3 view_dir_world = normalize((INV_VIEW_MATRIX * vec4(VERTEX, 0.0)).xyz);
	float vertical = thickness * clamp(abs(view_dir_world.y), 0.15, 1.0);

	float d = clamp(vertical / depth_scale, 0.0, 1.0);
	vec3 col = mix(srgb_to_linear(shallow_color), srgb_to_linear(deep_color), smoothstep(0.0, 1.0, d));

	float t = TIME;
	float fn = lb_noise(world_pos.xz * 7.0 + vec2(t * 0.4, -t * 0.3));
	float foam = 1.0 - smoothstep(0.0, foam_width * (0.55 + fn * 0.9), vertical);
	float bands = smoothstep(0.75, 1.0, sin(vertical / foam_width * 2.2 - t * 1.6 + fn * 2.0));
	foam = max(foam, bands * (1.0 - smoothstep(0.0, foam_width * 3.5, vertical)) * 0.55);

	// Animated ripple normals.
	vec2 grad = ripple_gradient(world_pos.xz, t) * ripple_strength * 0.06;
	vec3 n_world = normalize(vec3(-grad.x, 1.0, -grad.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(n_world, 0.0)).xyz);

	col = mix(col, srgb_to_linear(murk_color), murk * 0.85);
	foam *= 1.0 - murk * 0.75;

	// Winter ice on lakes, rivers and puddles: pale, opaque, still.
	float ice = can_freeze ? season_freeze : 0.0;
	float crack = smoothstep(0.47, 0.5, abs(lb_noise(world_pos.xz * 5.0) - 0.5) + 0.47);
	col = mix(col, mix(vec3(0.72, 0.84, 0.92), vec3(0.9, 0.95, 1.0), crack), ice * 0.85);
	foam *= 1.0 - ice;
	n_world = normalize(mix(n_world, vec3(0.0, 1.0, 0.0), ice));
	NORMAL = normalize((VIEW_MATRIX * vec4(n_world, 0.0)).xyz);
	ALBEDO = mix(col, srgb_to_linear(foam_color), foam);
	ALPHA = mix(mix(clamp(min_alpha + d * (1.0 - min_alpha) + murk * 0.3, 0.0, 1.0), 1.0, foam * 0.9), 0.97, ice);
	ROUGHNESS = mix(0.12, 0.6, foam);
	SPECULAR = 0.6;
	METALLIC = 0.0;
	if (far_fade > 0.0) {
		float dist = length(VERTEX);
		ALBEDO = mix(ALBEDO, srgb_to_linear(deep_color) * 1.15, smoothstep(far_fade * 0.5, far_fade, dist) * 0.5);
	}
}
