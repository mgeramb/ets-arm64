// knx-hidbridge (macOS): reicht ein USB-HID-Gerät (Standard: KNX License Stick 2a07:0102)
// per TCP an die Linux-VM weiter, wo uhid-bridge.py ein identisches virtuelles HID-Gerät anlegt.
//
// Bauen:  clang -O2 -o knx-hidbridge knx-hidbridge.c -framework IOKit -framework CoreFoundation
// Start:  ./knx-hidbridge [host] [port] [vid] [pid]     (Standard 127.0.0.1 7531 0x2a07 0x0102)
//
// Protokoll (beide Richtungen): [u8 typ][u32 länge LE][nutzdaten]
//  Mac->VM : 1 CREATE  {u32 vid,pid,version,country; char name[128]; char uniq[64]; u8 rdesc[]}
//            2 INPUT   {report (inkl. Report-ID, falls verwendet)}
//            5 GETREP_REPLY {u32 id; u16 err; u8 data[]}
//            7 SETREP_REPLY {u32 id; u16 err}
//  VM->Mac : 3 OUTPUT  {u8 rtype; u8 data[]}                 (rtype: 0=Feature 1=Output 2=Input, uhid-Zählung)
//            4 GETREP  {u32 id; u8 rnum; u8 rtype}
//            6 SETREP  {u32 id; u8 rnum; u8 rtype; u8 data[]}
#include <CoreFoundation/CoreFoundation.h>
#include <IOKit/hid/IOHIDManager.h>
#include <IOKit/hid/IOHIDKeys.h>
#include <arpa/inet.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <unistd.h>

static int sock = -1;
static IOHIDDeviceRef dev;
static int uses_report_ids;
static pthread_mutex_t send_lock = PTHREAD_MUTEX_INITIALIZER;
static uint8_t inbuf[4096];

static int send_msg(uint8_t type, const void *a, uint32_t alen, const void *b, uint32_t blen)
{
    uint8_t hdr[5]; uint32_t len = alen + blen; int ok = 1;
    hdr[0] = type; memcpy(hdr + 1, &len, 4);
    pthread_mutex_lock(&send_lock);
    if (write(sock, hdr, 5) != 5 || (alen && write(sock, a, alen) != (ssize_t)alen) ||
        (blen && write(sock, b, blen) != (ssize_t)blen)) ok = 0;
    pthread_mutex_unlock(&send_lock);
    return ok;
}

static int read_full(int fd, void *buf, size_t n)
{
    size_t got = 0; ssize_t r;
    while (got < n) { if ((r = read(fd, (char *)buf + got, n - got)) <= 0) return 0; got += r; }
    return 1;
}

static IOHIDReportType mac_type(uint8_t uhid_rtype)
{
    return uhid_rtype == 0 ? kIOHIDReportTypeFeature : uhid_rtype == 1 ? kIOHIDReportTypeOutput : kIOHIDReportTypeInput;
}

static void input_cb(void *ctx, IOReturn res, void *sender, IOHIDReportType type, uint32_t id, uint8_t *report, CFIndex len)
{
    if (res != kIOReturnSuccess || len <= 0) return;
    if (!send_msg(2, report, (uint32_t)len, NULL, 0)) { fprintf(stderr, "Verbindung verloren\n"); exit(2); }
}

static void removed_cb(void *ctx, IOReturn res, void *sender)
{
    fprintf(stderr, "Gerät entfernt – beende\n");
    exit(3);
}

static void *socket_reader(void *arg)
{
    uint8_t hdr[5], buf[4200]; uint32_t len;
    for (;;) {
        if (!read_full(sock, hdr, 5)) { fprintf(stderr, "VM hat Verbindung beendet\n"); exit(2); }
        memcpy(&len, hdr + 1, 4);
        if (len > sizeof(buf) || !read_full(sock, buf, len)) { fprintf(stderr, "Protokollfehler\n"); exit(2); }
        if (hdr[0] == 3 && len >= 1) {               /* OUTPUT */
            uint8_t *d = buf + 1; CFIndex n = len - 1; uint32_t rid = (uses_report_ids && n) ? d[0] : 0;
            IOReturn r = IOHIDDeviceSetReport(dev, mac_type(buf[0]), rid, d, n);
            if (r != kIOReturnSuccess) fprintf(stderr, "SetReport(output) Fehler 0x%x\n", r);
        } else if (hdr[0] == 4 && len >= 6) {        /* GET_REPORT */
            uint32_t id; uint8_t data[4096]; CFIndex n = sizeof(data); uint16_t err;
            memcpy(&id, buf, 4);
            IOReturn r = IOHIDDeviceGetReport(dev, mac_type(buf[5]), buf[4], data, &n);
            uint8_t rep[6]; err = r == kIOReturnSuccess ? 0 : 5 /* EIO */;
            memcpy(rep, &id, 4); memcpy(rep + 4, &err, 2);
            send_msg(5, rep, 6, data, err ? 0 : (uint32_t)n);
        } else if (hdr[0] == 6 && len >= 6) {        /* SET_REPORT */
            uint32_t id; uint16_t err; uint8_t rep[6];
            memcpy(&id, buf, 4);
            IOReturn r = IOHIDDeviceSetReport(dev, mac_type(buf[5]), buf[4], buf + 6, len - 6);
            err = r == kIOReturnSuccess ? 0 : 5;
            memcpy(rep, &id, 4); memcpy(rep + 4, &err, 2);
            send_msg(7, rep, 6, NULL, 0);
        }
    }
    return NULL;
}

static uint32_t num_prop(IOHIDDeviceRef d, CFStringRef key)
{
    CFTypeRef v = IOHIDDeviceGetProperty(d, key); int32_t n = 0;
    if (v && CFGetTypeID(v) == CFNumberGetTypeID()) CFNumberGetValue(v, kCFNumberSInt32Type, &n);
    return (uint32_t)n;
}

static void str_prop(IOHIDDeviceRef d, CFStringRef key, char *out, size_t n)
{
    CFTypeRef v = IOHIDDeviceGetProperty(d, key); out[0] = 0;
    if (v && CFGetTypeID(v) == CFStringGetTypeID()) CFStringGetCString(v, out, n, kCFStringEncodingUTF8);
}

int main(int argc, char **argv)
{
    const char *host = argc > 1 ? argv[1] : "127.0.0.1";
    int port = argc > 2 ? atoi(argv[2]) : 7531;
    int vid = argc > 3 ? (int)strtol(argv[3], 0, 0) : 0x2a07, pid = argc > 4 ? (int)strtol(argv[4], 0, 0) : 0x0102;

    IOHIDManagerRef mgr = IOHIDManagerCreate(kCFAllocatorDefault, kIOHIDOptionsTypeNone);
    CFMutableDictionaryRef match = CFDictionaryCreateMutable(NULL, 0, &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    CFNumberRef v = CFNumberCreate(NULL, kCFNumberIntType, &vid), p = CFNumberCreate(NULL, kCFNumberIntType, &pid);
    CFDictionarySetValue(match, CFSTR(kIOHIDVendorIDKey), v);
    CFDictionarySetValue(match, CFSTR(kIOHIDProductIDKey), p);
    IOHIDManagerSetDeviceMatching(mgr, match);
    IOHIDManagerOpen(mgr, kIOHIDOptionsTypeNone);
    CFSetRef set = IOHIDManagerCopyDevices(mgr);
    if (!set || CFSetGetCount(set) == 0) {
        fprintf(stderr, "HID-Gerät %04x:%04x nicht gefunden (in Parallels ggf. „mit Mac verbinden“ wählen)\n", vid, pid);
        return 1;
    }
    CFSetGetValues(set, (const void **)&dev);
    if (IOHIDDeviceOpen(dev, kIOHIDOptionsTypeNone) != kIOReturnSuccess) { fprintf(stderr, "IOHIDDeviceOpen fehlgeschlagen\n"); return 1; }

    CFDataRef rd = IOHIDDeviceGetProperty(dev, CFSTR(kIOHIDReportDescriptorKey));
    if (!rd) { fprintf(stderr, "kein Report-Deskriptor\n"); return 1; }
    const uint8_t *rdp = CFDataGetBytePtr(rd); CFIndex rdl = CFDataGetLength(rd);
    for (CFIndex i = 0; i < rdl; ) {                 /* Report-ID-Items (0x85) suchen */
        uint8_t b = rdp[i]; int sz = (b & 3) == 3 ? 4 : (b & 3);
        if ((b & 0xfc) == 0x84) uses_report_ids = 1;
        i += 1 + sz;
    }

    struct sockaddr_in sa = { .sin_family = AF_INET, .sin_port = htons(port) };
    inet_pton(AF_INET, host, &sa.sin_addr);
    for (int tries = 0; ; tries++) {
        sock = socket(AF_INET, SOCK_STREAM, 0);
        if (connect(sock, (struct sockaddr *)&sa, sizeof(sa)) == 0) break;
        close(sock);
        if (tries == 0) fprintf(stderr, "warte auf uhid-bridge in der VM (%s:%d) ...\n", host, port);
        sleep(2);
    }
    int one = 1; setsockopt(sock, IPPROTO_TCP, TCP_NODELAY, &one, sizeof(one));

    struct { uint32_t vid, pid, version, country; char name[128]; char uniq[64]; } __attribute__((packed)) c;
    memset(&c, 0, sizeof(c));
    c.vid = vid; c.pid = pid;
    c.version = num_prop(dev, CFSTR(kIOHIDVersionNumberKey));
    c.country = num_prop(dev, CFSTR(kIOHIDCountryCodeKey));
    str_prop(dev, CFSTR(kIOHIDProductKey), c.name, sizeof(c.name));
    str_prop(dev, CFSTR(kIOHIDSerialNumberKey), c.uniq, sizeof(c.uniq));
    send_msg(1, &c, sizeof(c), rdp, (uint32_t)rdl);
    fprintf(stderr, "verbunden: %s (%04x:%04x, Seriennr. %s, Deskriptor %ld Byte, Report-IDs %s)\n",
            c.name, vid, pid, c.uniq, (long)rdl, uses_report_ids ? "ja" : "nein");

    IOHIDDeviceRegisterInputReportCallback(dev, inbuf, sizeof(inbuf), input_cb, NULL);
    IOHIDDeviceRegisterRemovalCallback(dev, removed_cb, NULL);
    IOHIDDeviceScheduleWithRunLoop(dev, CFRunLoopGetCurrent(), kCFRunLoopDefaultMode);
    pthread_t t; pthread_create(&t, NULL, socket_reader, NULL);
    CFRunLoopRun();
    return 0;
}
