shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

#include "res://shaders/common.gdshaderinc"

// 0 = collapsed, 1 = fully grown. Props pop in one by one using the delay stored in CUSTOM0.w.
instance uniform float grow = 1.0;
instance uniform vec4 highlight : source_color = vec4(1.0, 1.0, 1.0, 0.0);
instance uniform float dim = 0.0;

uniform float wind_strength = 1.0;

varying float v_class;
varying vec3 v_world_normal;
varying vec2 v_pivot;

float ease_out_back(float t) {
	float c1 = 1.9;
	float c3 = c1 + 1.0;
	float u = t - 1.0;
	return 1.0 + c3 * u * u * u + c1 * u * u;
}

void vertex() {
	vec3 pivot = CUSTOM0.xyz;
	float delay = CUSTOM0.w;
	v_class = COLOR.a;
	v_pivot = (MODEL_MATRIX * vec4(pivot, 1.0)).xz;
	v_world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float t = clamp((grow - delay * 0.6) / 0.4, 0.0, 1.0);
	float s = max(ease_out_back(t), 0.0);
	vec3 local = (VERTEX - pivot) * s;
	// Buildings are constructed rather than popped: walls rise from the ground, then roofs drop on.
	float cls = COLOR.a;
	if (cls > 0.015 && cls < 0.045) {
		float rise = smoothstep(0.0, 1.0, t);
		local = VERTEX - pivot;
		local.y *= rise;
		local.xz *= mix(0.92, 1.0, rise);
	} else if (cls >= 0.045 && cls < 0.09) {
		float drop = smoothstep(0.55, 1.0, t);
		local = (VERTEX - pivot) * mix(0.8, 1.0, drop) + vec3(0.0, (1.0 - drop) * 0.3, 0.0);
	}

	float sway = COLOR.a;
	vec3 wp = (MODEL_MATRIX * vec4(pivot, 1.0)).xyz;
	float phase = TIME * 1.7 + wp.x * 1.3 + wp.z * 0.9;
	float gust = 0.6 + 0.4 * sin(TIME * 0.35 + wp.x * 0.2);
	float h = max(local.y, 0.0);
	local.x += sin(phase) * 0.05 * wind_strength * sway * h * gust;
	local.z += cos(phase * 0.83 + 1.0) * 0.035 * wind_strength * sway * h * gust;

	VERTEX = pivot + local;
}

// Vertex alpha classes (see Sway.cs): 0.5 bush, 0.68 flower, 0.8 evergreen, 0.9 grass, 1.0 deciduous.
vec3 seasonal(vec3 c) {
	float decid = step(0.95, v_class);
	float grass = step(0.85, v_class) * (1.0 - decid);
	float bush = step(0.45, v_class) * (1.0 - step(0.6, v_class));
	float lum = dot(c, vec3(0.3, 0.59, 0.11));
	float pick = lb_hash(v_pivot * 3.7);

	// Spring: fresh, light greens, and some crowns burst into pink or white blossom.
	c = mix(c, c * vec3(0.95, 1.18, 0.85), (decid + bush) * season_spring * 0.45);
	vec3 blossom = mix(vec3(0.92, 0.5, 0.62), vec3(0.95, 0.92, 0.9), lb_hash(v_pivot * 7.1));
	c = mix(c, blossom, decid * step(pick, 0.38) * season_spring * 0.8);
	// Autumn: deciduous crowns turn red, orange or gold; grass and shrubs brown.
	vec3 fall = mix(vec3(0.62, 0.12, 0.03), vec3(0.85, 0.48, 0.05), pick) * (0.7 + lum * 1.6);
	c = mix(c, fall, decid * season_autumn * 0.9);
	c = mix(c, c * vec3(1.25, 0.95, 0.5), (grass + bush) * season_autumn * 0.55);
	// Dry season: everything leafy goes straw-coloured.
	c = mix(c, c * vec3(1.3, 1.05, 0.45), (grass + decid * 0.6 + bush * 0.7) * season_dry * 0.7);
	// Winter: bare brown crowns, then snow on every upward face.
	c = mix(c, vec3(0.16, 0.12, 0.09), decid * season_snow * 0.6);
	float up = smoothstep(0.3, 0.75, v_world_normal.y);
	c = mix(c, vec3(0.86, 0.9, 0.96), season_snow * up * 0.92);
	return c;
}

void fragment() {
	vec3 c = seasonal(srgb_to_linear(COLOR.rgb));
	vec3 hl = srgb_to_linear(highlight.rgb);
	c = mix(c, hl, highlight.a * 0.3);
	ALBEDO = c * (1.0 - dim);
	ROUGHNESS = 0.85;
	SPECULAR = 0.3;
	EMISSION = hl * highlight.a * 0.18;
}
