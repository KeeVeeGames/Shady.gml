varying vec2 v_vTexcoord;
varying vec4 v_vColour;

#pragma shady: skip_compilation

#pragma shady: import(sh_example_exports.GRAYSCALE_FACTOR)
#pragma shady: import(sh_example_exports.grayscale)

#pragma shady: macro_begin DEFINITIONS

varying float v_Test;
uniform float u_Test;

/*
multi-line
comment
test
*/

// comment test

#pragma shady: macro_end

void main()
{
    #pragma shady: macro_begin FRAGCOLOR
        gl_FragColor = v_vColour * texture2D(gm_BaseTexture, v_vTexcoord);
    #pragma shady: macro_end
    
    #pragma shady: macro_begin INVERSE_GRAYSCALE
    
        #pragma shady: macro_begin INVERSE
            gl_FragColor = vec4(vec3(1.0 - gl_FragColor.rgb), gl_FragColor.a);
        #pragma shady: macro_end
    
        #pragma shady: macro_begin GRAYSCALE
            gl_FragColor = grayscale(gl_FragColor);
        #pragma shady: macro_end
    
    #pragma shady: macro_end
}
