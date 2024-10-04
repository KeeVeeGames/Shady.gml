varying vec2 v_vTexcoord;
varying vec4 v_vColour;

#pragma shady: import(sh_example_exports)

bool funcVariant()
{
    #ifdef VARIANCE2
        return true;
    #else
        return false;
    #endif
}

void main()
{
    vec4 color = texture2D( gm_BaseTexture, v_vTexcoord );
    
    #ifdef VARIANCE1
        color /= 2.0;
    #endif
    
    gl_FragColor = v_vColour * color;
}
