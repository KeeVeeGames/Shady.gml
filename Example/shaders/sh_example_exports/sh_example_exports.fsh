varying vec2 v_vTexcoord;
varying vec4 v_vColour;

float random(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
}

#define GRAYSCALE_FACTOR vec3(0.2126, 0.7152, 0.0722)
vec4 grayscale(vec4 color) {
    return vec4(vec3(dot(color.rgb, GRAYSCALE_FACTOR)), color.a);
}

vec2 flip(vec2 texcoord) {
    return vec2(texcoord.x, 1.0 - texcoord.y);
}

void main()
{
    vec4 color = texture2D(gm_BaseTexture, v_vTexcoord);
    
    gl_FragColor = v_vColour * color;
}
