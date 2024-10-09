# Shady.gml [![Donate](https://img.shields.io/badge/donate-%E2%9D%A4-blue.svg)](https://musnik.itch.io/donate-me) [![License](https://img.shields.io/github/license/KeeVeeGames/OKColor.gml)](#!)
<img align="left" src="https://keevee.games/wp-content/uploads/2024/10/logo-300x300.png" alt="Logo" width="150">

**Shady** is a GLSL preprocessor tool for GameMaker that allows you to include pieces of code from other shaders and generate shader variants for code reuse!

The tool is integrated into the compilation process via compiler scripts so you can write reusable shaders inside standard GameMaker shader files with built-in or any other code editor.

It is still in Beta and tested to work with **GLSL ES** language but should also work with **GLSL**. **HLSL** is not (yet?) supported.

## Installation
1. Download the latest executable from the [releases page](https://github.com/KeeVeeGames/Shady.gml/releases) for your OS and architecture.
2. Create a directory inside your project location alongside other resource directories and name it, for example, `#shady`.
3. Place the executable inside `#shady` directory.
4. Create or modify [compiler scripts](https://manual.gamemaker.io/monthly/en/Settings/Runner_Details/Compiler_Batch_Files.htm) in your project location to include the code:
<details>
  <summary><b>Windows Batch Files</b></summary>
  
  \
  `pre_build_step.bat`
  ```batch
  %~dp0\#shady\Shady %~dp0 --pre
  ```
  \
  `post_textures.bat`
  ```batch
  %~dp0\#shady\Shady %~dp0 --post
  ```
</details>

<details>
  <summary><b>Linux / MacOS Shell Scripts</b></summary>
  
  \
  `pre_build_step.sh`
  ```console
  #!/bin/bash
  
  ${0%/*}/#shady/Shady ${0%/*} --pre
  ```
  \
  `post_textures.sh`
  ```console
  #!/bin/bash

  ${0%/*}/#shady/Shady ${0%/*} --post
  ```
</details>

## How to use
**Shady** is using a custom `#pragma` syntax with special directives. This won't break the shader compiler as unknown custom pragmas are just ignored by it.

You can write shady directives right in the GameMaker shader files, both vertex and fragment.

Vertex and fragment shaders have separate databases so you can't import identifiers from vertex shader into fragment shader.

> [!NOTE]
> Shader files that are only used as a library to import to other shaders are still required to have a `main` function and standard `varying`s to not generate errors on syntax checking / compilation.
> Although this makes it possible to use those shaders as normal in the game.

#
* ### `import` directive:
**Syntax:**
```glsl
#pragma shady: import(shader_name)
#pragma shady: import(shader_name.identifier_name)
```

<details>
  <summary><b>Example</b></summary>
  
  \
  `sh_functions.fsh`
  ```glsl
  varying vec2 v_vTexcoord;  // ignored for import
  varying vec4 v_vColour;    // ignored for import

  float random(vec2 st) {
      return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
  }

  #define GRAYSCALE_FACTOR vec3(0.2126, 0.7152, 0.0722)
  vec4 grayscale(vec4 color) {
      return vec4(vec3(dot(color.rgb, GRAYSCALE_FACTOR)), color.a);
  }

  const vec2 textureScale = vec2(4096.0 / 1920.0, 4096.0 / 1080.0);

  // ignored for import
  void main() {
      vec4 color = texture2D(gm_BaseTexture, v_vTexcoord);
    
      gl_FragColor = v_vColour * color;
  }
  ```
  \
  `sh_shader.fsh`
  ```glsl
  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;

  #pragma shady: import(sh_functions)  // import everything from sh_functions (random, GRAYSCALE_FACTOR, grayscale, textureScale)
  #pragma shady: import(sh_functions.random)  // import specific function (random)
  #pragma shady: import(sh_functions.textureScale)  // import specific variable (textureScale)

  void main() {
      vec4 color = texture2D(gm_BaseTexture, v_vTexcoord);
    
      gl_FragColor = v_vColour * grayscale(color);  // you can then use imported stuff like it's there
  }
  ```
</details>

#

* ### Macros and `inline` directive:
**Syntax:**
```glsl
#pragma shady: macro_begin MACRO_NAME
#pragma shady: macro_end
#pragma shady: inline(shader_name.MACRO_NAME)
```

<details>
  <summary><b>Example</b></summary>
  
  \
  `sh_macros.fsh`
  ```glsl
  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;

  void main() {
      #pragma shady: macro_begin FRAGCOLOR
          gl_FragColor = v_vColour * texture2D(gm_BaseTexture, v_vTexcoord);
      #pragma shady: macro_end
  }
  ```
  \
  `sh_shader.fsh`
  ```glsl
  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;

  void main() {
      #pragma shady: inline(sh_macros.FRAGCOLOR)  // inline code from sh_macros FRAGCOLOR macro
  }
  ```
  \
  Nested macros are also supported, so this code:
  ```glsl
  #pragma shady: macro_begin INVERSE_GRAYSCALE
  
      #pragma shady: macro_begin INVERSE
          gl_FragColor = vec4(vec3(1.0 - gl_FragColor.rgb), gl_FragColor.a);
      #pragma shady: macro_end
  
      #pragma shady: macro_begin GRAYSCALE
          gl_FragColor = grayscale(gl_FragColor);
      #pragma shady: macro_end
  
  #pragma shady: macro_end
  ```
  Will generate three macros `INVERSE_GRAYSCALE`, `INVERSE` and `GRAYSCALE` that will all work.
</details>

#

* ### `variant` directive:
**Syntax:**
```glsl
#pragma shady: variant(shader_name, [KEYWORD_NAME1], [KEYWORD_NAME2], ...)
```

<details>
  <summary><b>Example</b></summary>
  
  \
  `sh_shader_base.fsh`
  ```glsl
  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;
  
  #pragma shady: import(sh_effects)
  
  void main()
  {
      #ifdef BLUR
          vec4 color = texture2DBlur(gm_BaseTexture, v_vTexcoord);
      #else
          vec4 color = texture2D(gm_BaseTexture, v_vTexcoord);
      #endif
      
      #ifdef NOISE
          color = noise(color);
      #endif
      
      #ifdef DARKEN
          color = darken(color);
      #endif
      
      gl_FragColor = v_vColour * color;
  }
  ```
  \
  `sh_shader_variant.fsh`
  ```glsl
  #pragma shady: variant(sh_shader_base, BLUR, DARKEN) // will generate a variant of sh_shader_base with BLUR and DARKEN enabled

  // the rest is ignored
  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;

  void main() {
      gl_FragColor = v_vColour * texture2D(gm_BaseTexture, v_vTexcoord);
  }
  ```
  \
  The variant directive with no keywords will create the exact copy of the original shader, which may be useful for generating code that share the same vertex shader, for example.
  \
  Original shader can also be used as normal.
</details>

## Alternatives:
* **[Xpanda](https://github.com/GameMakerDiscord/Xpanda)**. Uses a custom syntax and is not integrated with the compilation process but supports HLSL.
* **[glslfy](https://github.com/glslify/glslify)**. Older brother for non-GameMaker users.

## TODO:
* **Optimize caching to not rewrite unmodified shaders.**
* **Support more shader languages?**

## Author:
Nikita Musatov - [MusNik / KeeVee Games](https://twitter.com/keeveegames)
