# SPDX-License-Identifier: AGPL-3.0-only
"""Generate original, minimal format fixtures; no copyrighted song material."""
from pathlib import Path
import struct
import zipfile

root = Path(__file__).resolve().parents[1] / 'windows' / 'fixtures'

def integer(n): return struct.pack('<i', n)
def fixed(text, size):
    data = text.encode('ascii')
    return bytes([len(data)]) + data.ljust(size, b'\0')
def unused(text):
    data = text.encode('ascii')
    return integer(len(data)+1) + bytes([len(data)]) + data

def legacy(version):
    data = fixed(f'FICHIER GUITAR PRO v{version}.00', 30)
    fields = ['Rocker original format fixture', '', 'Rocker', '', '']
    if version == 5: fields += ['Rocker']
    fields += ['Original exercise; AGPL-3.0-only', 'Rocker', 'Four ascending chromatic notes']
    data += b''.join(unused(s) for s in fields) + integer(0)
    if version < 5: data += b'\0'
    if version >= 4: data += integer(0) + (integer(1)+integer(0))*5
    if version == 5: data += bytes(28) + struct.pack('<h', 0) + (integer(1)+b'\0')*10 + integer(1)+b'\0'
    data += integer(120) + integer(0)
    if version >= 4: data += b'\0'
    data += (integer(24) + bytes([12,8]) + bytes(6))*64
    if version == 5: data += struct.pack('<h', -1)*19 + bytes(4)
    data += integer(1) + integer(1)  # one master bar and one track
    data += bytes([3,4,4])
    if version == 5: data += bytes(7)  # beams, alternate, triplet, padding
    data += bytes([0x80]) + fixed('Kytara',40) + integer(6)
    data += b''.join(integer(n) for n in [64,59,55,50,45,40,0])
    data += b''.join(integer(n) for n in [1,1,2,24,0]) + bytes([120,80,40,0])
    if version == 5:
        data += bytes([3,1,0,0,0]) + integer(0)+integer(0)+integer(100)+bytes(range(1,11))+bytes([255,3])+bytes(16)
        data += b'\0'
    data += integer(4)
    for fret in range(4):
        data += bytes([0,0,2,0x20,1,fret])
        if version == 5: data += bytes(3)
    if version == 5: data += integer(0)  # empty second voice
    return data

for version in (3,4,5): (root / f'notation-original.gp{version}').write_bytes(legacy(version))
with zipfile.ZipFile(root / 'notation-phrases.gp') as source:
    gpif = source.read('Content/score.gpif')
# Uncompressed GP6 filesystem: directory sector + zero-terminated sector table.
blocks = (len(gpif)+4095)//4096
fs = bytearray((2+blocks)*4096)
struct.pack_into('<i',fs,4096,2)
fs[4100:4110] = b'score.gpif'
struct.pack_into('<i',fs,4096+0x8c,len(gpif))
for index in range(blocks): struct.pack_into('<i',fs,4096+0x94+index*4,index+2)
fs[8192:8192+len(gpif)] = gpif
(root / 'notation-original.gpx').write_bytes(b'BCFS'+fs)
with zipfile.ZipFile(root / 'notation-original.mxl','w',zipfile.ZIP_DEFLATED) as dest:
    for name, data in [('META-INF/container.xml',b'<?xml version="1.0"?><container><rootfiles><rootfile full-path="score.musicxml" media-type="application/vnd.recordare.musicxml+xml"/></rootfiles></container>'),('score.musicxml',(root/'notation-scale.musicxml').read_bytes())]:
        info=zipfile.ZipInfo(name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;dest.writestr(info,data)
