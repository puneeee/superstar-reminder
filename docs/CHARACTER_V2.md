# Expressive Superstar companion

The stock character now uses 24 transparent, illustrated Rajinikanth poses rather than the older small photographic loop. The generated likeness is a stylized tribute, not footage or an exact reproduction of a film performance.

## Reminder sequence

1. A deliberate six-pose swagger walk brings him in from the configured bottom corner (about 1.15 seconds).
2. He settles for 0.45 seconds, lifts/twirls/replaces his sunglasses, then pauses briefly.
3. The speech bubble fades in above his head. Only then does the 30-second response period begin.
4. While awaiting your response he cycles through relaxed, attentive poses.
5. **Yes:** pleased approval, a three-pose temple salute, then a brisk grounded walk across the screen. No running animation is selected.
6. **No / Snooze / no response:** a surprised/disappointed reaction and lowered head, a farewell salute, then a slower, head-down walk back toward the arrival edge. The sad reaction remains visible before he departs.

Responses are persisted before the farewell animation. Cancelling a presentation closes both windows and stops sound. A snooze schedules the reminder as before; the animation does not alter its selected duration.

## Size and spacing

The stock height is 30% of usable desktop height, with an 18–40% Settings slider and a 440-logical-pixel ceiling. An existing stock 18% preference upgrades automatically; deliberately chosen alternatives are retained within the supported rendering range. The notification uses its measured content height and a speech-tail anchor near the character's head, rather than a fixed 245-pixel offset. The intended head-to-bubble gap is approximately 6 logical pixels, plus the cutout's small transparent inset. Monitor scaling is applied once at the window boundary.

## Assets and generation

Built-in image-generation tool; transparent atlas, 24 full-body poses, 6 columns × 4 rows. Art-direction prompt: recognizable stylized Rajinikanth, silver swept-back hair, salt-and-pepper beard, black sunglasses, ivory rolled-sleeve shirt, charcoal trousers and black shoes; consistent full-body 3D illustration. Six grounded swagger-walk poses; landed stance; lift, twirl and replace glasses; attentive idle poses; pleased thumbs-up; three temple-salute poses; surprise, disappointment and head-down walking. No text, background, borders or props besides spectacles.

The final PNGs are in `src/SuperstarReminder/Assets/Characters/Rajinikanth/expressive/00.png` through `23.png`. `character.json` maps poses to clips with per-clip frame rate and loop flags. Non-looping gestures hold their final pose. Legacy character packages fall back to their idle/walk clips for gestures they do not provide.

`tools/extract_character_atlas.py` extracts complete alpha-connected bodies instead of slicing through feet at approximate grid boundaries, retains detached spectacles, and uses a shared scale and foot baseline. It does not synthesize artwork. Run it with the original generated atlas and an output directory (Pillow/numpy/scipy required only for regenerating assets; the app needs none of these).

## Verify on your desktop

Exit the old app from the tray before replacing the extracted application folder with the new build. Your database and per-user settings are outside that folder. Run **Preview character** first. Check size and spacing at your actual display scaling, watch the full arrival before the bubble appears, then try Yes, No and Snooze. The six-frame walk is illustrated pose animation; exact film-grade Rajinikanth gait/motion capture is not claimed.
