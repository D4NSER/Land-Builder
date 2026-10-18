// Shared helpers for Land Builder shaders.

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

// Seasons, driven from C# (SeasonDirector) and shared by every material.
global uniform float season_snow;
global uniform float season_autumn;
global uniform float season_spring;
global uniform float season_dry;
global uniform float season_freeze;

float lb_hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float lb_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = lb_hash(i);
	float b = lb_hash(i + vec2(1.0, 0.0));
	float c = lb_hash(i + vec2(0.0, 1.0));
	float d = lb_hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}
