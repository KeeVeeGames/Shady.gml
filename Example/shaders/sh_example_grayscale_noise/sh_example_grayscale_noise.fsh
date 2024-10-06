#pragma shady: import(sh_example_exports)

varying vec2 v_vTexcoord;
varying vec4 v_vColour;

void main()
{
    gl_FragColor = v_vColour * grayscale(texture2D(gm_BaseTexture, v_vTexcoord)) * (1.0 - random(v_vTexcoord) / 2.0);
}