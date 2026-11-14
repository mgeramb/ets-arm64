import struct,sys
sys.path.insert(0, __file__.rsplit('/',1)[0])
from pescan import rva_to_off, cstr
d=open(sys.argv[1],'rb').read(); pe=struct.unpack_from('<I',d,0x3c)[0]
nsec=struct.unpack_from('<H',d,pe+6)[0]; osz=struct.unpack_from('<H',d,pe+20)[0]; opt=pe+24
magic=struct.unpack_from('<H',d,opt)[0]; dd=opt+(96 if magic==0x10b else 112)
secs=[]
for i in range(nsec):
    s=opt+osz+i*40; vs,va,rs,ro=struct.unpack_from('<IIII',d,s+8); secs.append((va,vs,ro,rs))
erva,esz=struct.unpack_from('<II',d,dd)
o=rva_to_off(erva,secs); n=struct.unpack_from('<I',d,o+24)[0]; nrva=struct.unpack_from('<I',d,o+32)[0]
no=rva_to_off(nrva,secs)
for i in range(n): print(cstr(d,rva_to_off(struct.unpack_from('<I',d,no+4*i)[0],secs)))
