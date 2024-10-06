varying vec2 v_vTexcoord;
varying vec4 v_vColour;

#pragma shady: import(sh_example_macros)

void main()
{
    #pragma shady: inline(sh_example_macros.FRAGCOLOR)
    #pragma shady: inline(sh_example_macros.INVERSE_GRAYSCALE)
}