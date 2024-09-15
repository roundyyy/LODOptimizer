# LOD Optimizer

**LOD Optimizer** is a Unity Editor tool designed to optimize the Last Level of Detail (LOD) by creating texture atlases, significantly reducing draw calls for distant objects. This tool is especially effective when used with prefabs, streamlining your workflow and enhancing scene performance.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
  - [Workflow](#workflow)
- [Settings Explained](#settings-explained)
- [Recommendations](#recommendations)
- [Additional Information](#additional-information)

## Features

- **LOD Level Optimization:** Automatically replaces the highest LOD levels with optimized meshes using texture atlases to minimize draw calls.
- **Prefab Compatibility:** Designed to work seamlessly with prefabs, ensuring optimized LOD levels across your project.
- **Flexible Atlasing:** Can also function as a standard atlassing tool, supporting meshes without LOD groups.
- **Material Color Application:** Option to apply material colors to the atlas, enhancing visual consistency.
- **Normal Map Packing:** Supports packing normal maps into atlases for enhanced visual fidelity without significant performance overhead.
- **Automatic Folder Management:** Organizes optimized assets into a structured folder hierarchy within your project.
- **UV Adjustment Warnings:** Detects and warns about UVs outside the 0-1 range, ensuring texture tiling issues are minimized.

## Installation

1. **Download the Tool:**
   - Clone or download the repository to your local machine.

2. **Import into Unity:**
   - Place LODOptimizer inside the Editor folder (if you don't have Editor folder, just creat one)

3. **Access the Tool:**
   - In Unity, go to `Tools > LOD Optimizer` to open the optimization window.

## Usage

### Workflow

1. **Add Prefabs to Scene:**
   - Drag and drop the prefabs you wish to optimize into your current scene.

2. **Select Prefabs for Optimization:**
   - In the **LOD Optimizer** window, click on `Add Selected Objects` to populate the list with your chosen prefabs.
   - Alternatively, manually add objects using the provided list.

3. **Configure Settings:**
   - Adjust the atlas size, material names, shader selection, padding, and other settings as per your optimization needs.

4. **Apply Optimization:**
   - Click on `Optimize LOD` to begin the optimization process.
   - The tool will replace the highest LOD levels with optimized meshes and create texture atlases.

5. **Apply Changes to Prefabs:**
   - After optimization, manually apply the changes to your prefabs to ensure they use the optimized assets.

6. **Enable Static Batching:**
   - Mark your prefabs as static by selecting them and enabling the `Static` checkbox in the Inspector.
   - Static batching works best with atlased objects, further reducing draw calls.

7. **Use Optimized Prefabs:**
   - Your prefabs are now optimized and can be used as usual. The tool ensures that the highest LOD levels are static batched, reducing draw calls significantly.

## Settings Explained

### Max Atlas Size

- **Description:** Defines the maximum resolution of the generated texture atlases.
- **Options:** 256, 512, 1024, 2048, 4096.
- **Recommendation:** Choose the smallest atlas size that accommodates all textures to optimize memory usage without compromising visual quality.

### Material Name

- **Description:** Specifies the name of the material that will use the generated texture atlas.
- **Default:** `LODOptimized_Material`.
- **Usage:** Helps in organizing and identifying materials created by the optimizer.

### Target Shader

- **Description:** Selects the shader to be applied to the atlas material.
- **Default:** Unity's Standard Shader.
- **Recommendation:** Use basic shaders for the highest LOD levels to minimize performance overhead.

### Apply Material Color

- **Description:** When enabled, the original material's color is applied to the atlas texture.
- **Default:** Enabled.
- **Usage:** Useful for maintaining color consistency across different objects when using the atlas.

### Padding (px)

- **Description:** Sets the padding in pixels between individual textures within the atlas.
- **Default:** 1 pixel.
- **Recommendation:** Adjust padding to prevent texture bleeding, especially if textures have borders or gradients.

### Enable Normal Map Packing

- **Description:** When enabled, the tool also packs normal maps into a separate atlas.
- **Default:** Disabled.
- **Usage:** Enhances surface detail on optimized objects without increasing draw calls.
- **Recommendation:** Enable only if normal maps are essential for the visual fidelity of distant objects.

### Normal Atlas Material Name

- **Description:** Specifies the name for the generated normal map atlas material.
- **Default:** `LODOptimized_NormalMaterial`.
- **Usage:** Organizes normal map materials separately for better asset management.

## Recommendations

- **Static Batching:**
  - After optimization, mark your prefabs as static by selecting them and enabling the `Static` checkbox in the Inspector. Static batching works best with atlased objects, further reducing draw calls.

- **Normal Maps for Highest LODs:**
  - For the highest LOD levels (i.e., the most distant objects), normal maps are often not necessary. Using a basic shader with the smallest possible texture size can further reduce performance costs.

- **Shader Selection:**
  - Opt for lightweight shaders when working with atlased objects to maximize performance gains.

- **Texture Size Management:**
  - Use the smallest atlas size that still maintains acceptable visual quality to conserve memory and improve rendering performance.

- **Prefab Workflow:**
  - Incorporate this tool early in your prefab workflow to ensure all prefabs are optimized consistently, leading to significant performance improvements in large scenes.

## Additional Information

### Code Logic and Features

- **Atlas Creation:**
  - Automatically calculates the optimal grid layout for texture atlases based on the number of unique textures and their sizes.
  - Supports both diffuse and normal maps, packing them into separate atlases if normal map packing is enabled.

- **Mesh Duplication and UV Remapping:**
  - Duplicates original meshes to preserve the original assets.
  - Remaps UV coordinates to fit within the newly created texture atlas grid, ensuring textures align correctly.

- **Material Assignment:**
  - Creates new materials that use the generated texture atlases and assigns them to the optimized meshes.
  - Handles shader properties such as `_MainTex` for diffuse maps and `_BumpMap` for normal maps.

- **Folder Structure Management:**
  - Automatically creates and organizes assets into a structured folder hierarchy (`LODOptimizerFolder > Meshes`, `Textures`, `Materials`).

- **UV Warnings:**
  - Detects meshes with UVs outside the 0-1 range and adjusts them to prevent texture tiling issues, providing warnings to the user.
