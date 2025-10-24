# Shady.gml [![Donate](https://img.shields.io/badge/donate-%E2%9D%A4-blue.svg)](https://musnik.itch.io/donate-me) [![License](https://img.shields.io/github/license/KeeVeeGames/OKColor.gml)](#!)
<img align="left" src="https://keevee.games/wp-content/uploads/2024/10/logo-300x300.png" alt="Logo" width="150">

**Shady** is a GLSL preprocessor tool for GameMaker that allows you to include pieces of code from other shaders and generate shader variants for code reuse! [**Support on Itch.io.**](https://musnik.itch.io/shady)

The tool is integrated into the compilation process via compiler scripts so you can write reusable shaders inside standard GameMaker shader files with built-in or any other code editor.

\
It is still in Beta and tested to work with **GLSL ES** language but should also work with **GLSL**. **HLSL** is not (yet?) supported.

> [!NOTE]
> The tool is **fully cross-platform**, meaning that the resulting shaders will work on any platform (**Desktop, Mobile, Consoles, Web**) if they are written correctly for that. The different executable builds that are distributed with this tool are intended for the platform that the **developer** is using. For example, if you're using GameMaker on M2 Mac use `mac-arm64` Shady build, if you're on Windows use `win-x64` build and so on. The compiled shaders will work anywhere!

> [!WARNING]
> Always use source control or backups for your projects that use Shady, although very unlikely, the software may corrupt the shader files and you can lost your work.

## Installation

1. Download yymps extension file from the [release page](https://github.com/KeeVeeGames/Shady.gml/releases) and import it to your GameMaker project.
2. Download the executable zip archive from the release page for your OS and architecture.
3. Unzip and place the executable into the extension path (`YourProject/extensions/Shady`).
4. You may also want to add these lines to `.gitignore` to remove temp Shady files from Git:

```gitignore
*.fsh_mod
*.vsh_mod
```

## How to use
**Shady** is using a custom `#pragma` syntax with special directives. This isn't breaking the standard shader compiler as unknown custom pragmas are just ignored by it.

You can write shady directives right in the GameMaker shader files, both vertex and fragment.

By default, you're importing definitions from the same type of shader (fragment to fragment, vertex to vertex), but you can also explicitly specify the extension (`.fsh`/`.vsh`) of the shader you are importing from.

> [!NOTE]
> Shader files that are only used as a library to import to other shaders still has to be correct, compilable and require to have a `main` function.
> If you are not planning to use that "library" shader in-game as an effect, use `skip_compilation` directive that will replace all of the shader's code with a blank `main` function, but will still remain reachable for Shady directives.


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
  You can import functions, variables and `#define`s. However, `varying`s, `uniform`s and `main` function are not exported.\
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
  Will generate three macros: `INVERSE_GRAYSCALE`, `INVERSE` and `GRAYSCALE` that will all work.
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
      // use #ifdef, #else, #if defined() or #elif defined() to define variant keywords
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

#

* ### `skip_compilation` directive:
**Syntax:**
```glsl
#pragma shady: skip_compilation
```

<details>
  <summary><b>Example</b></summary>
  
  \
  `sh_utility.fsh`
  ```glsl
  #pragma shady: skip_compilation

  varying vec2 v_vTexcoord;
  varying vec4 v_vColour;
  
  float random(vec2 st) {
      return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
  }

  #define GRAYSCALE_FACTOR vec3(0.2126, 0.7152, 0.0722)
  vec4 grayscale(vec4 color) {
      return vec4(vec3(dot(color.rgb, GRAYSCALE_FACTOR)), color.a);
  }

  const vec2 textureScale = vec2(4096.0 / 1920.0, 4096.0 / 1080.0);
  
  void main()
  {
      gl_FragColor = v_vColour * texture2D(gm_BaseTexture, v_vTexcoord);
  }
  ```
  This will create a "stub" shader without any code in it to compile by GameMaker's shader compiler, but you will still be able to import/inline/variant from it. Learn more in [#5](https://github.com/KeeVeeGames/Shady.gml/issues/5).
  \
  \
  `sh_utility.fsh_mod`
  ```glsl
  // shader skipped by skip_compilation
  void main() {}
  ```
</details>

## Troubleshooting
* **Defender<sup>tm</sup> is too defensive**. Some anti-viruses may yield a false-positive warning on the binaries. There's nothing I can do for now besides waiting for binaries to get trusted over time or getting a paid code sign certificate, which is not cost-effective for the current state of the project. If you're not sure, you can compile the tool yourself from sources using Visual Studio and .NET 8.0.
* **"Project Directory Modified"**. To not see that GameMaker message-box every time any shader is processed navigate to `Preferences` > `General Settings` and enable `Automatically reload changed files`.
  <details>
    <summary><b>Screenshot</b></summary>

    ![image](https://github.com/user-attachments/assets/8ca4f138-bc2a-478c-b23b-046b94e8eee4)

  </details>
* **Changes in the shader aren't applying / shader has lost its code**. Use `Clean` option for the Shady executable to flush possibly corrupted cache files. It is hooked to the "Brush" button in GameMaker IDE so you can just press that. Alternatively, you can call shady executable with the `--clean` option.
* **GameMaker's shader compilation errors are reporting the wrong line number**. As the preprocessor is "expanding" your added pragma directives and GameMaker's compiler is compiling already expanded sources, you will get error reports on the wrong lines that are from the modified expanded shader source. To view the modified shader to get the idea of what may have gone wrong, navigate to the "shaders" folder of the project, find the directory that has the name of the bugged shader and open the file with the `_mod` postfix inside the directory.

## Alternatives and inspirations:
* **[Xpanda](https://github.com/GameMakerDiscord/Xpanda)** – uses a custom syntax and is not integrated with the compilation process but supports HLSL.
* **[glslfy](https://github.com/glslify/glslify)** – elder brother for non-GameMaker users.
* **[Unity Shader Variants](https://docs.unity3d.com/Manual/shader-variants.html)**.

## TODO:
* **~Optimize caching to not rewrite unmodified shaders.~**
* **Support more shader languages?**

## Author:
Nikita Musatov - [MusNik / KeeVee Games](https://twitter.com/keeveegames)
