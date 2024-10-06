varying vec2 v_vTexcoord;
varying vec4 v_vColour;

void main()
{
    #pragma shady: inline(sh_example_macros.FRAGCOLOR)
    #pragma shady: inline(sh_example_macros.INVERSE)
}