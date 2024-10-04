varying vec2 v_vTexcoord;
varying vec4 v_vColour;

bool otherfunc()
{
    return false;
}

#pragma shady: import(sh_example.someVar)
#pragma shady: import(sh_example_exports.exportVar)

void main()
{
    gl_FragColor = v_vColour * texture2D( gm_BaseTexture, v_vTexcoord );
}
