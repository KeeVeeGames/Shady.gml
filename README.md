# Shady.gml [![Donate](https://img.shields.io/badge/donate-%E2%9D%A4-blue.svg)](https://musnik.itch.io/donate-me) [![License](https://img.shields.io/github/license/KeeVeeGames/OKColor.gml)](#!)
<img align="left" src="https://keevee.games/wp-content/uploads/2024/10/logo-300x300.png" alt="Logo" width="150">

**Shady** is a GLSL preprocessor tool for GameMaker that allows you to include pieces of code from other shaders and generate shader variants for code reuse!

The tool is integrated into the compilation process via compiler scripts so you can write reusable shaders inside standard GameMaker shader files with built-in or any other code editor.

\
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
  "%~dp0\#shady\Shady" "%~dp0." --pre
  ```
  \
  `post_textures.bat`
  ```batch
  "%~dp0\#shady\Shady" "%~dp0." --post
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

5. You may also want to add these lines to `.gitignore` to remove temp Shady files from Git:

```gitignore
*.fsh_mod
*.vsh_mod
```

## How to use
**Shady** is using a custom `#pragma` syntax with special directives. This isn't breaking the standard shader compiler as unknown custom pragmas are just ignored by it.

You can write shady directives right in the GameMaker shader files, both vertex and fragment.

Vertex and fragment shaders have separate databases so you can't import identifiers from vertex shader into fragment shader.

> [!NOTE]
> Shader files that are only used as a library to import to other shaders are still required to have a `main` function (possibly blank) to not generate errors on compilation.\
> Although, as those shaders are compiled and presented in the project it is possible to utilize them for something useful in the game.

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
  float random(vec2 st) {
      return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
  }

  #define GRAYSCALE_FACTOR vec3(0.2126, 0.7152, 0.0722)
  vec4 grayscale(vec4 color) {
      return vec4(vec3(dot(color.rgb, GRAYSCALE_FACTOR)), color.a);
  }

  const vec2 textureScale = vec2(4096.0 / 1920.0, 4096.0 / 1080.0);

  void main() {}
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
  \
  You can import functions, variables and `#define`s. `varying`s, `uniform`s and `main` function are not exported.\
  Nested imports are also supported, so `A` imports `B` which imports `C`, with duplicate imports resolved.
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
  `sh_megashader.fsh`
  ```glsl
  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;
  
  #pragma shady: import(sh_effects)
  
  void main()
  {
      // use #ifdef, #if defined() or #elif defined() to define variant keywords
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
  #pragma shady: variant(sh_shader_base, BLUR, DARKEN) // will generate a variant of sh_megashader with BLUR and DARKEN enabled

  // any code after is ignored and will be replaced with the source shader code with enabled keywords
  ```
  \
  The variant directive with no keywords will create the exact copy of the original shader, which may be useful for generating code that shares the same vertex shader, for example.
  \
  The original shader can also be used as normal.
</details>

## Troubleshooting
* **Defender<sup>tm</sup> is too defensive**. Some anti-viruses may yield a false-positive warning on the binaries. There's nothing I can do for now besides waiting for binaries to get trusted over time or getting a paid code sign certificate, which is not cost-effective for the current state of the project. If you're not sure, you can compile the tool yourself from sources using Visual Studio and .NET 8.0.
* **"Project Directory Modified"**. To not see that GameMaker message-box every time any shader is processed navigate to `Preferences` > `General Settings` and enable `Automatically reload changed files`.
  <details>
    <summary><b>Screenshot</b></summary>

    ![image](https://github.com/user-attachments/assets/8ca4f138-bc2a-478c-b23b-046b94e8eee4)

  </details>

## Alternatives and inspirations:
* **[Xpanda](https://github.com/GameMakerDiscord/Xpanda)** – uses a custom syntax and is not integrated with the compilation process but supports HLSL.
* **[glslfy](https://github.com/glslify/glslify)** – older brother for non-GameMaker users.
* **[Unity Shader Variants](https://docs.unity3d.com/Manual/shader-variants.html)**.

## TODO:
* **~Optimize caching to not rewrite unmodified shaders.~**
* **Support more shader languages?**

## Author:
Nikita Musatov - [MusNik / KeeVee Games](https://twitter.com/keeveegames)
