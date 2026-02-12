# Creating Visual Assets

This guide helps you create the visual elements for the Corn Hole game.

## Hole Visual

The hole should look like a dark circle on the ground that grows as it consumes objects.

### Basic Approach (No Custom Shaders)

1. **Create Hole Material**:
   - Right-click in Project > Create > Material
   - Name: "HoleMaterial"
   - Set color to dark gray/black (RGB: 20, 20, 20)
   - Set Metallic: 0
   - Set Smoothness: 0.2

2. **Apply to Cylinder**:
   - On HolePlayer prefab's visual child
   - Assign HoleMaterial to the mesh renderer

### Advanced Approach (With Custom Shader)

For a more realistic "hole" effect:

1. **Create Portal/Hole Shader**:
   - Use Unity's Shader Graph or:
   - Create Unlit shader with transparency
   - Add scrolling/swirl effect
   - Add depth fade

2. **Glow Effect**:
   - Add emission to material
   - Set emission color to dark purple/blue
   - Adjust HDR intensity

Example Shader Graph setup:
```
UV > Tiling and Offset > Rotate > Gradient Noise > Color
```

## Consumable Objects

Make objects visually distinct by size and value.

### Small Objects (10 points)
- **Cube**: Scale 0.5
- **Color**: Green (RGB: 50, 200, 50)
- **Material**: Standard, slight emission

### Medium Objects (25 points)
- **Cube**: Scale 1.0
- **Color**: Yellow (RGB: 200, 200, 50)
- **Material**: Standard, metallic

### Large Objects (50 points)
- **Sphere**: Scale 1.5
- **Color**: Red (RGB: 200, 50, 50)
- **Material**: Standard, shiny

### Very Rare (100 points)
- **Star/Special shape**: Scale 2.0
- **Color**: Gold (RGB: 255, 215, 0)
- **Material**: Metallic, high smoothness
- **Add glow**: Emission enabled

## Ground

### Simple Ground
1. Create Material: "GroundMaterial"
2. Use tiling texture (grass, sand, or grid)
3. Set tiling to (10, 10)
4. Apply to Plane

### Stylized Ground
- Use flat colors
- Add slight noise texture
- Consider cell-shaded look

## Environment

### Sky
- Window > Rendering > Lighting
- Set Skybox Material (default is fine)
- Or create solid color skybox

### Lighting
- Directional Light (sun)
  - Color: Slightly warm white
  - Intensity: 1
  - Rotation: (50, -30, 0)

### Fog (Optional)
- Edit > Project Settings > Quality
- Enable Fog
- Fog Color: Light blue/gray
- Fog Mode: Exponential
- Fog Density: 0.01

## Particles

### Consume Effect

When object is eaten, add particles:

1. **Create Particle System**:
   - GameObject > Effects > Particle System
   - Name: "ConsumeEffect"

2. **Configure**:
   ```
   Duration: 0.5
   Start Lifetime: 0.3
   Start Speed: 5
   Start Size: 0.2
   Emission: Burst 20
   Shape: Sphere, Radius: 0.5
   Color: Match consumed object
   ```

3. **Save as Prefab**

4. **Instantiate in Code**:
   ```csharp
   // In ConsumableObject.cs RPC_PlayConsumeEffect()
   Instantiate(consumeEffectPrefab, transform.position, Quaternion.identity);
   ```

### Growth Effect

When hole grows, add visual feedback:

1. **Create Particle Ring**:
   - Particle System
   - Shape: Circle, Radius: current hole size
   - Emit outward briefly

## UI Assets

### Menu Background
- Solid color or gradient
- Dark theme recommended
- Color: (30, 30, 40)

### Buttons
- Standard UI Button
- Color: (70, 130, 180) - Steel Blue
- Hover: Slightly lighter
- Pressed: Slightly darker

### Text
- Use TextMeshPro
- Font: Default or import custom font
- Color: White or light gray
- Size: 24-36 for UI, 18 for HUD

## Mobile-Specific

### Touch Indicator

Show where player is touching:

1. **Create Sprite**:
   - Circle sprite
   - Semi-transparent
   - Color: White with alpha 0.3

2. **Position at Touch Point**:
   ```csharp
   if (Input.touchCount > 0)
   {
       Vector3 touchPos = Input.GetTouch(0).position;
       touchIndicator.position = Camera.main.ScreenToWorldPoint(touchPos);
   }
   ```

### Virtual Joystick (Optional)

For alternative control scheme:
- Use Unity UI Image as background
- UI Image as handle
- Script to track drag

## Performance Tips

### Mobile Optimization

1. **Use Mobile Shaders**:
   - Mobile/Diffuse
   - Mobile/Unlit
   - Avoid complex shaders

2. **Reduce Particles**:
   - Lower max particles
   - Shorter lifetime
   - Reduce emission

3. **Texture Size**:
   - 512x512 or 1024x1024 max
   - Enable compression
   - Use appropriate format (ASTC, ETC2)

4. **Lighting**:
   - Use baked lighting where possible
   - Limit real-time lights
   - Disable shadows on mobile if needed

## Asset Checklist

Create these assets for a complete game:

- [ ] HoleMaterial (dark, slightly emissive)
- [ ] GroundMaterial (textured or colored)
- [ ] SmallConsumableMaterial (green)
- [ ] MediumConsumableMaterial (yellow)
- [ ] LargeConsumableMaterial (red)
- [ ] RareConsumableMaterial (gold)
- [ ] ConsumeParticleEffect prefab
- [ ] GrowthParticleEffect prefab
- [ ] UI Background sprite
- [ ] Button sprite (normal, hover, pressed)
- [ ] Touch indicator sprite
- [ ] Skybox material

## Example Color Scheme

### Dark Theme
- **Background**: RGB(20, 20, 30)
- **Primary**: RGB(70, 130, 180) - Steel Blue
- **Accent**: RGB(255, 140, 0) - Dark Orange
- **Text**: RGB(240, 240, 240) - Off White

### Bright Theme
- **Background**: RGB(135, 206, 235) - Sky Blue
- **Primary**: RGB(34, 139, 34) - Forest Green
- **Accent**: RGB(255, 69, 0) - Orange Red
- **Text**: RGB(20, 20, 20) - Dark Gray

## Resources

Free Asset Sources:
- **Textures**: textures.com, opengameart.org
- **Models**: sketchfab.com (free CC models)
- **Particles**: Unity Particle Pack (Asset Store)
- **UI**: Unity UI Extensions (Asset Store)
- **Fonts**: Google Fonts

Unity Asset Store (Free):
- "Simple UI Pack"
- "Particle Ribbon"
- "Cartoon FX"
