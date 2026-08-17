shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform sampler2D day_gradient; 
uniform float time_of_day : hint_range(0.0, 1.0) = 0.5; 
uniform float intensity : hint_range(0.0, 1.0) = 1.0; 
uniform float saturation : hint_range(0.0, 2.0) = 1.0; 

void fragment() {
    vec4 original_color = texture(screen_texture, SCREEN_UV);
    vec4 tint_color = texture(day_gradient, vec2(time_of_day, 0.5));
    float luminance = dot(original_color.rgb, vec3(0.299, 0.587, 0.114));
    vec3 grayscale = vec3(luminance);
    vec3 saturated_color = mix(grayscale, original_color.rgb, saturation);
    vec3 tinted_color = saturated_color * tint_color.rgb;
    vec3 final_color = mix(original_color.rgb, tinted_color, intensity);
    COLOR = vec4(final_color, 1.0);
}