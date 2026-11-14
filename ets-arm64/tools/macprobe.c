// Probes macOS/arm64 properties relevant for running Windows ARM64 code natively.
#include <errno.h>
#include <pthread.h>
#include <sched.h>
#include <signal.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

static volatile int got_signal;
static volatile uint64_t x18_in_handler;

static void handler(int sig) {
    uint64_t v;
    __asm__ volatile("mov %0, x18" : "=r"(v));
    x18_in_handler = v;
    got_signal = 1;
}

static void *burn(void *arg) {
    volatile uint64_t s = 0;
    for (;;) s++;
    return NULL;
}

int main(void) {
    printf("page size: %ld\n", sysconf(_SC_PAGESIZE));

    // --- x18 survival across preemption / syscalls / signals
    pthread_t t[16];
    for (int i = 0; i < 16; i++) pthread_create(&t[i], NULL, burn, NULL);

    const uint64_t magic = 0x5EB0A11C0FFEE018ULL;
    uint64_t lost_after = 0, before, after;
    for (uint64_t i = 1; i <= 200000000ULL; i++) {
        __asm__ volatile("mov x18, %1\n\t"
                         "mov x9, #0\n"
                         "1: add x9, x9, #1\n\t"
                         "cmp x9, #64\n\t"
                         "b.ne 1b\n\t"
                         "mov %0, x18\n\t"
                         : "=r"(after) : "r"(magic) : "x9", "x18", "cc");
        if (after != magic) { lost_after = i; before = magic; break; }
    }
    if (lost_after)
        printf("x18 busy loop: CLOBBERED after %llu iterations (now 0x%llx)\n",
               (unsigned long long)lost_after, (unsigned long long)after);
    else
        printf("x18 busy loop: preserved over 200M iterations with 16 competing threads\n");

    // across an explicit syscall + yield
    int clobber_sys = 0;
    for (int i = 0; i < 100000; i++) {
        __asm__ volatile("mov x18, %0" :: "r"(magic) : "x18");
        sched_yield();
        getpid();
        __asm__ volatile("mov %0, x18" : "=r"(after));
        if (after != magic) { clobber_sys++; }
    }
    printf("x18 across syscalls/yield: clobbered %d of 100000 times (last 0x%llx)\n",
           clobber_sys, (unsigned long long)after);

    // in a signal handler
    signal(SIGUSR1, handler);
    __asm__ volatile("mov x18, %0" :: "r"(magic) : "x18");
    raise(SIGUSR1);
    __asm__ volatile("mov %0, x18" : "=r"(after));
    printf("x18 in signal handler: 0x%llx, after return: 0x%llx\n",
           (unsigned long long)x18_in_handler, (unsigned long long)after);

    // --- memory: RWX without MAP_JIT, 4K-granular mprotect
    void *p = mmap(NULL, 65536, PROT_READ | PROT_WRITE | PROT_EXEC, MAP_PRIVATE | MAP_ANON, -1, 0);
    printf("mmap RWX (no MAP_JIT): %s\n", p == MAP_FAILED ? strerror(errno) : "ok");
    void *q = mmap(NULL, 65536, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANON, -1, 0);
    int r = mprotect((char *)q + 4096, 4096, PROT_READ);
    printf("mprotect at 4K offset: %s\n", r ? strerror(errno) : "ok (but rounds to 16K?)");
    void *f = mmap((void *)0x10000000ULL, 4096, PROT_READ, MAP_PRIVATE | MAP_ANON | MAP_FIXED, -1, 0);
    printf("MAP_FIXED at 4K-aligned non-16K address 0x10001000: %s\n",
           mmap((void *)0x10001000ULL, 4096, PROT_READ, MAP_PRIVATE | MAP_ANON | MAP_FIXED, -1, 0) == MAP_FAILED
               ? strerror(errno) : "ok");
    (void)f;
    printf("low address 0x10000 (Windows needs low 2GB/4GB): %s\n",
           mmap((void *)0x10000ULL, 65536, PROT_READ, MAP_PRIVATE | MAP_ANON | MAP_FIXED, -1, 0) == MAP_FAILED
               ? strerror(errno) : "ok");
    return 0;
}
