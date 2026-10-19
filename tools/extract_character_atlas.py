"""Extract the 24 generated transparent bodies; do not slice through feet at grid boundaries.
Usage: python tools/extract_character_atlas.py atlas.png output-directory
Requires Pillow, numpy, scipy. No artwork is synthesized by this script.
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import label, find_objects, binary_dilation
image = Image.open(sys.argv[1]).convert('RGBA')
rgba = np.array(image)
labels, _ = label(rgba[:, :, 3] > 100)
parts = []
for index, bounds in enumerate(find_objects(labels), 1):
    count = int((labels[bounds] == index).sum())
    if count > 1000:
        parts.append((count, index, bounds))
parts = sorted(parts, reverse=True)[:24]
assert len(parts) == 24, 'Expected exactly 24 full-body sprites'
parts.sort(key=lambda p: ((p[2][0].start+p[2][0].stop)/2 // (image.height/4), (p[2][1].start+p[2][1].stop)/2))
sprites = []
for _, index, bounds in parts:
    mask = labels == index
    body_row = int(((bounds[0].start+bounds[0].stop)/2) // (image.height/4))
    body_col = int(((bounds[1].start+bounds[1].stop)/2) // (image.width/6))
    # Keep detached dark accessories (the twirled spectacles) with their body.
    for other, box in enumerate(find_objects(labels), 1):
        if other == index: continue
        component = labels[box] == other
        size = int(component.sum())
        if not 20 <= size < 1000: continue
        row = int(((box[0].start+box[0].stop)/2) // (image.height/4))
        col = int(((box[1].start+box[1].stop)/2) // (image.width/6))
        if (row,col) != (body_row,body_col): continue
        colors = rgba[box][:,:,:3][component]
        if float(colors.mean()) < 100:
            mask |= labels == other
    mask = binary_dilation(mask, iterations=2)
    alpha = np.where(mask, rgba[:, :, 3], 0).astype('uint8')
    sprite = image.copy()
    sprite.putalpha(Image.fromarray(alpha))
    sprites.append(sprite.crop(sprite.getbbox()))
scale = min(270/max(x.width for x in sprites), 450/max(x.height for x in sprites))
output = Path(sys.argv[2]); output.mkdir(parents=True, exist_ok=True)
for index, sprite in enumerate(sprites):
    sprite = sprite.resize((round(sprite.width*scale), round(sprite.height*scale)), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (300, 480))
    canvas.alpha_composite(sprite, ((300-sprite.width)//2, 472-sprite.height))
    canvas.save(output/f'{index:02}.png', optimize=True)
print(f'Extracted {len(sprites)} consistent-foot-anchor frames.')
