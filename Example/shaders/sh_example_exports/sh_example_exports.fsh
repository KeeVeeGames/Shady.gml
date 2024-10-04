varying vec2 v_vTexcoord;
varying vec4 v_vColour;

#pragma shady: import(sh_example.func)
#pragma shady: import(sh_example.someVar)
#pragma shady: import(sh_example.SOME_DEFINE)

#pragma shady: import(sh_example_otherfunc)

vec3 exportVar = vec3(0.0, 0.0, 0.0);

void main()
{
    #pragma shady: macro_begin FRAGCOLOR
    
    #pragma shady: macro_begin TEXTURE
    vec4 color = texture2D( gm_BaseTexture, v_vTexcoord );
    #pragma shady: macro_end
    
    gl_FragColor = v_vColour * color;
    
    #pragma shady: macro_end
}
