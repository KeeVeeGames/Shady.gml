varying vec2 v_vTexcoord;
varying vec4 v_vColour;

#pragma shady: import(sh_example_exports.random)
#pragma shady: import(sh_example_exports.vsh.flip)

vec4 color_channel(vec4 color)
{
    #ifdef RED
        return vec4(color.r, 0.0, 0.0, color.a);
    #elif defined(GREEN)
        return vec4(0.0, color.g, 0.0, color.a);
    #elif defined(BLUE)
        return vec4(0.0, 0.0, color.b, color.a);
    #endif
    
    return color;
}

void main()
{
    vec4 color = color_channel(texture2D(gm_BaseTexture, flip(v_vTexcoord)));
    
    #ifdef NOISE
        color *= (1.0 - random(v_vTexcoord) / 2.0);
    #endif
    
    gl_FragColor = v_vColour * color;
}
