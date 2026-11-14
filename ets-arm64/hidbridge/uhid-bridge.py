#!/usr/bin/env python3
"""uhid-bridge (Linux-VM, als root): nimmt die Verbindung von knx-hidbridge (macOS) an und legt über
/dev/uhid ein identisches virtuelles HID-Gerät an (-> /dev/hidrawN mit gleicher VID/PID/Seriennummer).

Aufruf: sudo python3 uhid-bridge.py [port]   (Standard 7531; Lima leitet den Port auf 127.0.0.1 des Macs)
Protokoll siehe knx-hidbridge.c.
"""
import os, select, socket, struct, sys

UHID_DESTROY, UHID_START, UHID_STOP, UHID_OPEN, UHID_CLOSE, UHID_OUTPUT = 1, 2, 3, 4, 5, 6
UHID_GET_REPORT, UHID_GET_REPORT_REPLY, UHID_CREATE2, UHID_INPUT2 = 9, 10, 11, 12
UHID_SET_REPORT, UHID_SET_REPORT_REPLY = 13, 14
EV_SIZE = 4 + 4372          # sizeof(struct uhid_event)
BUS_USB = 3


def ev(etype, payload=b""):
    return (struct.pack("<I", etype) + payload).ljust(EV_SIZE, b"\0")


def recv_full(s, n):
    buf = b""
    while len(buf) < n:
        chunk = s.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("Mac-Seite getrennt")
        buf += chunk
    return buf


def recv_msg(s):
    t, ln = struct.unpack("<BI", recv_full(s, 5))
    return t, recv_full(s, ln) if ln else b""


def send_msg(s, t, data):
    s.sendall(struct.pack("<BI", t, len(data)) + data)


def serve(conn):
    t, data = recv_msg(conn)
    if t != 1:
        raise ValueError("erwarte CREATE")
    vid, pid, version, country = struct.unpack_from("<IIII", data)
    name = data[16:144].split(b"\0")[0]
    uniq = data[144:208].split(b"\0")[0]
    rdesc = data[208:]
    print(f"Gerät: {name.decode(errors='replace')} {vid:04x}:{pid:04x} Seriennr. {uniq.decode()} "
          f"Deskriptor {len(rdesc)} Byte", flush=True)
    fd = os.open("/dev/uhid", os.O_RDWR | os.O_CLOEXEC)
    try:
        create = struct.pack("<128s64s64sHHIIII", name, b"knx-hidbridge", uniq, len(rdesc), BUS_USB,
                             vid, pid, version, country) + rdesc
        os.write(fd, ev(UHID_CREATE2, create))
        while True:
            r, _, _ = select.select([conn, fd], [], [])
            if conn in r:
                t, data = recv_msg(conn)
                if t == 2:                                   # INPUT
                    os.write(fd, ev(UHID_INPUT2, struct.pack("<H", len(data)) + data))
                elif t == 5:                                 # GET_REPORT reply
                    rid, err = struct.unpack_from("<IH", data)
                    payload = data[6:]
                    os.write(fd, ev(UHID_GET_REPORT_REPLY, struct.pack("<IHH", rid, err, len(payload)) + payload))
                elif t == 7:                                 # SET_REPORT reply
                    rid, err = struct.unpack_from("<IH", data)
                    os.write(fd, ev(UHID_SET_REPORT_REPLY, struct.pack("<IH", rid, err)))
            if fd in r:
                e = os.read(fd, EV_SIZE)
                etype = struct.unpack_from("<I", e)[0]
                if etype == UHID_OUTPUT:
                    size, rtype = struct.unpack_from("<HB", e, 4 + 4096)
                    send_msg(conn, 3, bytes([rtype]) + e[4:4 + size])
                elif etype == UHID_GET_REPORT:
                    rid, rnum, rtype = struct.unpack_from("<IBB", e, 4)
                    send_msg(conn, 4, struct.pack("<IBB", rid, rnum, rtype))
                elif etype == UHID_SET_REPORT:
                    rid, rnum, rtype, size = struct.unpack_from("<IBBH", e, 4)
                    send_msg(conn, 6, struct.pack("<IBB", rid, rnum, rtype) + e[12:12 + size])
                elif etype in (UHID_START, UHID_OPEN, UHID_CLOSE, UHID_STOP):
                    print({UHID_START: "start", UHID_OPEN: "open", UHID_CLOSE: "close", UHID_STOP: "stop"}[etype], flush=True)
    finally:
        try:
            os.write(fd, ev(UHID_DESTROY))
        except OSError:
            pass
        os.close(fd)
        print("virtuelles Gerät entfernt", flush=True)


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 7531
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("0.0.0.0", port))
    srv.listen(1)
    print(f"uhid-bridge wartet auf Port {port}", flush=True)
    while True:
        conn, addr = srv.accept()
        conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        print(f"Verbindung von {addr}", flush=True)
        try:
            serve(conn)
        except (ConnectionError, OSError, ValueError, struct.error) as ex:
            print(f"Ende: {ex}", flush=True)
        finally:
            conn.close()


if __name__ == "__main__":
    main()
