#pragma shady: import(sh_example_exports)

varying vec2 v_vTexcoord;
varying vec4 v_vColour;

#define SOME_DEFINE 0
vec2 someVar = vec2(0.0, 0.0);

bool func()
{
    return true;
}

void main()
{
    #ifdef SOME_DEFINE
        #pragma shady: inline(sh_example_exports.FRAGCOLOR)
    #endif
}