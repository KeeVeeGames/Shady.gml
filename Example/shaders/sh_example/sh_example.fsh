#pragma shady: import(sh_postprocess)
#pragma shady: import(sh_postprocess.TextureFXAA)
#pragma shady: inline(sh_postprocess.MACRO)
#pragma shady: variant(sh_postprocess, GL_OES_standard_derivatives)
#pragma shady: macro_begin MACRO
#pragma shady: macro_end

varying vec2 v_vTexcoord;
varying vec4 v_vColour;

void main()
{
    gl_FragColor = v_vColour * texture2D( gm_BaseTexture, v_vTexcoord );
}
