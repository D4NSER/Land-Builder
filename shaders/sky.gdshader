shader_type sky;

// Painterly gradient sky with a soft sun and slow procedural clouds.
uniform vec3 top_color : source_color = vec3(0.35, 0.65, 0.86);
uniform vec3 horizon_color : source_color = vec3(0.81, 0.91, 0.95);
uniform vec3 ground_color : source_color = vec3(0.16, 0.5, 0.65);
uniform vec3 sun_tint : source_color = vec3(1.0, 0.95, 0.86);
uniform vec3 cloud_color : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 cloud_shade : source_color = vec3(0.78, 0.84, 0.92);
uniform float cloud_coverage = 0.5;
uniform float cloud_speed = 0.006;

float sky_hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float sky_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(sky_hash(i), sky_hash(i + vec2(1.0, 0.0)), u.x), mix(sky_hash(i + vec2(0.0, 1.0)), sky_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float sky_fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		v += sky_noise(p) * a;
		p = p * 2.03 + vec2(17.1, -9.3);
		a *= 0.5;
	}
	return v;
}

void sky() {
	vec3 dir = EYEDIR;
	float h = dir.y;
	vec3 col = h >= 0.0
		? mix(horizon_color, top_color, smoothstep(0.0, 0.55, h))
		: mix(horizon_color, ground_color, smoothstep(0.0, 0.25, -h));

	if (LIGHT0_ENABLED) {
		float d = max(dot(dir, LIGHT0_DIRECTION), 0.0);
		col += sun_tint * (pow(d, 900.0) * 6.0 + pow(d, 40.0) * 0.35 + pow(d, 6.0) * 0.12) * LIGHT0_ENERGY * 0.6;
	}

	if (h > 0.0) {
		vec2 uv = dir.xz / (h + 0.12) * 0.9;
		float t = TIME * cloud_speed;
		float n = sky_fbm(uv + vec2(t, t * 0.4));
		float cover = smoothstep(1.0 - cloud_coverage, 1.0 - cloud_coverage + 0.22, n);
		float shade = smoothstep(0.35, 0.85, sky_fbm(uv * 1.7 + vec2(t * 1.3, 3.0)));
		vec3 cloud = mix(cloud_shade, cloud_color, shade);
		col = mix(col, cloud, cover * smoothstep(0.02, 0.18, h) * 0.9);
	}

	COLOR = col;
}
