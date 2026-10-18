"""Generate REWIND RAMPAGE's original fourteen-segment header typeface.

Authoring helper only. Requires fontTools; Unity players use the baked TTF/SDF assets.
"""
from pathlib import Path
import math
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

# Coordinates and masks are authored for this project, not copied from a font.
segments = {
    'a': ((95, 720), (465, 720)), 'b': ((490, 690), (490, 390)),
    'c': ((490, 330), (490, 30)), 'd': ((95, 0), (465, 0)),
    'e': ((70, 30), (70, 330)), 'f': ((70, 390), (70, 690)),
    'g': ((100, 360), (250, 360)), 'h': ((310, 360), (460, 360)),
    'i': ((100, 680), (250, 400)), 'j': ((280, 680), (280, 400)),
    'k': ((460, 680), (310, 400)), 'l': ((100, 40), (250, 320)),
    'm': ((280, 40), (280, 320)), 'n': ((460, 40), (310, 320)),
}
masks = {
    '0': 'abcdef', '1': 'bc', '2': 'abghde', '3': 'abcdgh',
    '4': 'fghbc', '5': 'afghcd', '6': 'afedcgh', '7': 'abc',
    '8': 'abcdefgh', '9': 'abcfghd',
    'A': 'abcefgh', 'B': 'afedcghjm', 'C': 'afed', 'D': 'abcdjm',
    'E': 'afedgh', 'F': 'afegh', 'G': 'afedch', 'H': 'bcefgh',
    'I': 'adjm', 'J': 'bcde', 'K': 'efkl', 'L': 'fed',
    'M': 'bcefik', 'N': 'bcefin', 'O': 'abcdef', 'P': 'abefgh',
    'Q': 'abcdefn', 'R': 'abefghn', 'S': 'afghcd', 'T': 'ajm',
    'U': 'bcdef', 'V': 'efkl', 'W': 'bcefln', 'X': 'ikln',
    'Y': 'ikm', 'Z': 'akld', '-': 'gh',
}

def polygon(pen, points):
    # Clockwise outlines for reliable TrueType/SDF rasterization.
    area = sum(a[0]*b[1] - b[0]*a[1] for a,b in zip(points, points[1:]+points[:1]))
    if area > 0:
        points = list(reversed(points))
    pen.moveTo(tuple(round(v) for v in points[0]))
    for point in points[1:]:
        pen.lineTo(tuple(round(v) for v in point))
    pen.closePath()

def glyph(character):
    pen = TTGlyphPen(None)
    for key in masks.get(character.upper(), ''):
        a,b = segments[key]
        dx,dy = b[0]-a[0], b[1]-a[1]
        length = math.hypot(dx,dy); ux,uy = dx/length, dy/length
        nx,ny = -uy*24, ux*24
        polygon(pen, [(a[0],a[1]), (a[0]+ux*22+nx,a[1]+uy*22+ny),
                      (b[0]-ux*22+nx,b[1]-uy*22+ny), (b[0],b[1]),
                      (b[0]-ux*22-nx,b[1]-uy*22-ny), (a[0]+ux*22-nx,a[1]+uy*22-ny)])
    if character in '.:':
        for cy in ([30] if character == '.' else [180,540]):
            polygon(pen, [(255,cy-22),(255,cy+22),(299,cy+22),(299,cy-22)])
    return pen.glyph()

def main():
    output = Path(__file__).resolve().parents[1] / 'Art' / 'TubeDisplay.ttf'
    characters = ' ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-:'
    names = {c: 'uni%04X' % ord(c) for c in characters}
    order = ['.notdef'] + list(names.values())
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap({ord(c): n for c,n in names.items()})
    builder.setupGlyf({'.notdef': glyph('0'), **{n:glyph(c) for c,n in names.items()}})
    builder.setupHorizontalMetrics({n:(620,46) for n in order})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({'familyName':'RR Tube Display', 'styleName':'Regular',
        'uniqueFontIdentifier':'REWIND RAMPAGE Tube Display 1.0',
        'fullName':'RR Tube Display Regular', 'psName':'RRTubeDisplay-Regular',
        'version':'Version 1.0', 'copyright':'Original segment geometry for REWIND RAMPAGE.'})
    builder.setupOS2(sTypoAscender=800,sTypoDescender=-200,usWinAscent=800,usWinDescent=200,
                    sxHeight=720,sCapHeight=720,fsType=0)
    builder.setupPost(); builder.setupMaxp(); builder.save(output)
    print(output)

if __name__ == '__main__':
    main()
