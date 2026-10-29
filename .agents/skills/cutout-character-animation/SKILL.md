---
name: cutout-character-animation
description: "Create 2D character animations by preparing separate body parts, assembling a cutout or skeletal rig, keyframing actions, and capturing transparent frames or sprite sheets. Use for character idle, walk, run, attack, and similar actions with stable appearance. Excludes generic static sprites, particle effects, and folded-sheet assets."
---

# Cutout character animation

Build the character once from reusable parts, animate the assembled rig, then capture its poses.
Do not generate each finished frame or a complete animation sheet independently. Image generation
may create consistent source parts or replacement drawings; motion and frame assembly come from
the rig and timeline. Reuse supplied layered artwork and existing character rigs before rebuilding.

## Establish the character contract

Inspect the character reference, animation consumer, and [asset guide](../../../Docs/Assets.md).
Determine requested actions and views, style/palette, frame size, PPU or scale, anchor, timing, loop
behavior, and in-place versus root motion. Infer missing values from the project and state assumptions;
do not invent an additional action set. Use one design reference to preserve identity and proportions.

Choose the simplest available 2D animation tool that meets the delivery requirements. In this Unity
project, use a `SpriteRenderer` transform hierarchy and animation clips for rigid parts; use the
installed 2D Animation package's meshes, bones, and weights when parts need deformation. Check the
package version and actual tools before relying on an API. A scriptable 2D compositor or an existing
external rigging tool is suitable for offline capture if no Unity rig is requested.

## Prepare and assemble parts

1. Split at meaningful moving joints: torso/pelvis, head, arms, hands, legs, feet, and accessories
   only as needed by the action. Separate upper/lower limbs when elbows or knees articulate. Use
   explicit left/right and front/back names; keep consistent scale, line weight, palette, and light.
2. Preserve transparent edges. Extend covered artwork beneath joints and clothing so rotation
   reveals complete shapes, not cut-off stumps or background holes. Reconstruct hidden areas before
   rigging. Avoid visible interior outlines across overlapping joints. If creating raster parts with
   an image tool, follow that tool's workflow and use the same character reference for each part.
3. Retain a neutral assembled reference. Record each part's file/layer, parent, local joint pivot,
   rest offset, and draw order in the editable rig or a small manifest. State the pixel-coordinate
   origin when serializing positions. A pivot is the anatomical joint, not the image rectangle center.
4. Build a hierarchy from a stable character root through hips/torso to limbs and attachments. Keep
   rendering order independent of parenting where needed. Animate joint rotations first; add mesh
   deformation or IK only where rigid parts cannot produce the required bend or foot placement.
5. Test the extreme poses before polishing motion. Check joint coverage, elbows/knees, attachment
   pivots, and draw order. For turns or strong foreshortening, use view-specific parts or replacement
   drawings within the rig; do not force a single flat limb through an impossible rotation. Preserve
   asymmetric costume details rather than blindly mirroring directions.

## Animate the character

Block the action's readable key poses first, then refine timing and spacing. Add only the secondary
motion needed for the request. Keep one authority driving each transform to avoid competing tracks.

For locomotion, plan contact, passing, and lift poses. Maintain planted feet during stance and match
stride to movement speed when the game applies translation. Keep intended jumps and airborne poses;
do not align every frame's feet to the ground. For attacks and similar one-shots, preserve anticipation,
action, and recovery rather than spreading the poses uniformly by default.

Looping actions must meet in pose and motion at the boundary. Test transitions between requested
actions when they share a runtime character. Correct a bad part, pivot, weight, or keyframe at its
source and recapture; independent frame repainting can reintroduce identity drift.

## Capture and package

1. Evaluate the timeline at exact sample times, not wall-clock screenshot intervals. With `N` loop
   frames at `fps`, sample `t = i / fps` for `i = 0 ... N-1`; use the matching pose at `N / fps` to
   close the source animation without exporting a duplicate endpoint. Preserve explicit holds and
   per-frame durations when timing is variable.
2. Use a fixed camera, canvas, scale, and root anchor covering all requested motion. Isolate the
   character from UI, gizmos, and background geometry. Capture PNG RGBA through the tool's renderer
   or a transparent render target, verifying that the active pipeline actually preserves alpha.
   Keep lighting, random effects, and simulation deterministic when they affect the capture.
3. Preserve natural size and position changes within the shared canvas. Do not independently crop,
   resize, or recenter each pose. If the consumer requires trimmed frames, retain offsets and source
   dimensions so the original anchor can be reconstructed.
4. For pixel art, use the target pixel grid and nearest-neighbor resizing; inspect rotated joints for
   uneven pixel density. Use replacement pixel poses when rotation breaks the style. For smooth art,
   use consistent filtering and any supersampling across the entire clip.
5. Export the requested frame sequence or sheet with documented order, timing, pivot convention,
   and frame dimensions. Keep atlas padding/extrusion compatible with filtering and slicing; use
   rotated packing only when the consumer supports it. Retain the parts and editable rig/timeline.
   A GIF or video is a playback preview, not the source of production transparency.

## Acceptance and delivery

- Compare representative poses against the neutral reference for consistent identity, proportions,
  colors, line weight, attachments, and view direction.
- Play each requested action at its intended speed, including the loop seam, and inspect problem
  frames slowly. Check joint gaps, wrong overlap order, clipping, unintended foot sliding, alpha
  halos, frame jitter, and an accidental extra pause at the loop boundary.
- Confirm frame count/order, RGBA data, canvas/anchor consistency, and timing in the actual exports.
  Re-import and check the real character consumer when Unity integration is requested; preserve
  existing GUIDs and audit slicing/subasset references when replacing a sheet.
- Deliver only the requested actions/views, their frame or sheet files, required metadata, a playback
  preview, and the reusable parts/rig source. Report paths, completed checks, and any unverified
  runtime behavior. Keep authoring sources and preview files outside shipping dependencies.

## Sources

Use documentation matching the installed tool/package version; these links describe the underlying
layer, pivot, and rigging workflows rather than requiring another engine.

- [Unity: preparing layered artwork](https://docs.unity3d.com/Packages/com.unity.2d.animation@13.0/manual/PreparingArtwork.html)
- [Unity: rigging a character made from multiple sprites](https://docs.unity3d.com/Packages/com.unity.2d.animation@13.0/manual/ex-psd-importer.html)
- [Godot: cutout hierarchy, joint pivots, draw order, and keyframing](https://docs.godotengine.org/en/stable/tutorials/animation/cutout_animation.html)
