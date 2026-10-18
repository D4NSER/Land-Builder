shader_type canvas_item;

// Soft corner darkening that draws the eye to the island.
uniform float strength = 0.32;
uniform vec4 tint : source_color = vec4(0.06, 0.1, 0.16, 1.0);

void fragment() {
	vec2 uv = UV - 0.5;
	uv.x *= 1.15;
	float v = smoothstep(0.32, 0.86, length(uv));
	COLOR = vec4(tint.rgb, v * strength);
}
