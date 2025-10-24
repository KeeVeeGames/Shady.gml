attribute vec3 in_Position;                  // (x,y,z)
attribute vec4 in_Colour;                    // (r,g,b,a)
attribute vec2 in_TextureCoord;              // (u,v)

varying vec2 v_vTexcoord;
varying vec4 v_vColour;

vec2 flip(vec2 st) {
    return vec2(st.x, 1.0 - st.y);
}

void main() {
    gl_Position = gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION] * vec4(in_Position.xyz, 1.0);
    
    v_vColour = in_Colour;
    v_vTexcoord = in_TextureCoord;
}