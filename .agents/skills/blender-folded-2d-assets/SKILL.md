---
name: blender-folded-2d-assets
description: "Create 2D assets of folded paper, bent cards, curled pages, and similar thin sheets by mapping flat artwork onto a plane, shaping it in Blender, and rendering it. Use when the visible result depends on folds, curvature, thickness, or self-occlusion. Excludes ordinary flat illustration and character animation."
---

# Blender folded 2D assets

Construct the shape in Blender and use its render as the final asset. Image generation may supply
the unfolded surface artwork or texture; do not ask it to invent the finished fold. Preserve supplied
artwork, lettering, and markings through UV mapping.

## Establish the asset contract

Read the relevant consumer and [asset guide](../../../Docs/Assets.md). Establish dimensions, aspect
ratio, transparency, viewing angle, scale/pivot, and whether the output is a still or animation.
Reuse established conventions and state reasonable assumptions for unspecified values.

Locate Blender and check its version before choosing APIs or modifier settings. Prefer a saved
scene and a reproducible `bpy` script for repeated variants or frame capture; UI authoring is also
valid. If Blender is unavailable, report the missing dependency instead of substituting a generated
picture of the fold. Keep editable masters and previews outside shipping asset dependencies.

## Build, shape, render

1. Prepare a flat front image at sufficient resolution for its final visible size. Keep exact text
   and diagrams in an editable source. Establish the reverse side explicitly: use supplied back art
   or a suitable plain paper material rather than accidentally repeating the front design.
2. Create a plane matching the image's proportions. Map UVs over the flat sheet before deformation,
   preserving them as the sheet folds. A temporary numbered checker texture helps verify orientation
   and stretching. Remove nonuniform object scale before configuring deformation.
3. Choose topology for the physical shape. For a sharp crease, place edges on the fold line and
   rotate the affected panel about that line using mesh transforms, hinge controls, or bones. Keep
   the panels rigid and connected at the crease. For a curl, add subdivisions along the curvature
   and use a controlled bend or curve. Do not smooth away deliberate creases.
4. Verify the deformation in a side view. Simple Deform uses local axes and its origin; an
   incorrectly oriented flat plane can show no bend. Orient the mesh or a deformation-origin Empty
   deliberately, then test a large angle before tuning the final pose. Prefer controlled transforms
   over cloth simulation for a specified fold; use simulation only when the requested motion needs it.
5. Add subtle thickness and an edge material only when visible at the target scale. Inspect modifier
   order, normals, reverse-side appearance, self-intersections, and coincident surfaces. Folding must
   not stretch the printed design or expose an unintended mirrored copy.
6. Choose an orthographic camera for a sprite or UI asset unless the reference needs perspective.
   Use an angle that reveals the intended shape. Match the consumer's color treatment and lighting;
   let geometry create fold shading instead of baking contradictory shadows into the flat image.
7. Render a low-cost proof of the final pose and any animation extremes. Fix shape and framing
   before increasing resolution or samples. For animation, bound the full motion with one camera,
   scale, canvas, and anchor; do not auto-fit the camera independently for each frame.
8. Capture through Blender's renderer with Film Transparent and PNG RGBA, rather than a viewport
   screenshot. Keep a contact shadow separate when the consumer needs one. Save the `.blend` with
   packed textures or portable relative dependencies and retain the script when scripting was used.

For animation, sample explicit timeline positions at the requested cadence. For a loop of duration
`T = N / fps`, export samples `t = i / fps` for `i = 0 ... N-1`; the pose at `T` closes the cycle but
is not an extra duplicate frame. A one-shot may need an explicit final hold. Pack frames only when
the consumer requires a sheet; retain frame order, timing, canvas dimensions, and pivot metadata.

## Acceptance and delivery

- Inspect the actual output at its intended display size. Confirm silhouette, crease location,
  curvature, thickness, legible markings, reverse side, and plausible occlusion against the reference.
- Check RGBA edges over dark and light backgrounds. Reject baked checkerboards, matte fringes,
  clipped edges, missing backs, texture stretching, and intersections visible at the target scale.
- For motion, inspect playback and intermediate frames for clipping, fold penetration, flicker,
  camera drift, and an unintended loop pause. Fix the scene and recapture affected frames.
- Deliver the requested PNG or animation files and a reproducible editable source. When project
  integration is requested, apply the import, ownership, and runtime checks in the asset guide.
  Report saved paths and the checks actually completed.

## Sources

Consult the documentation matching the installed Blender version when implementation details differ.

- [UV mapping](https://docs.blender.org/manual/en/latest/editors/uv/introduction.html)
- [Simple Deform and its axis/origin behavior](https://docs.blender.org/manual/en/latest/modeling/modifiers/deform/simple_deform.html)
- [Camera projection](https://docs.blender.org/manual/en/latest/render/cameras.html)
- [Transparent film](https://docs.blender.org/manual/en/latest/render/cycles/render_settings/film.html)
