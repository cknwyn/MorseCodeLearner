; ============================================================================
; benchmark.asm - cycle-accurate telemetry
;
; ULONGLONG Morse_GetBenchmarkCycles(void)
;   Returns the processor time-stamp counter (RDTSC) as one 64-bit value.
;   The caller reads it before and after a native call and subtracts to get the
;   elapsed cycles. RDTSC returns the high half in EDX and the low half in EAX;
;   they are merged into RAX per the Microsoft x64 return convention.
;   Note: RDTSC is not serialised, so very small deltas include pipeline noise.
; ============================================================================
.code
PUBLIC Morse_GetBenchmarkCycles
Morse_GetBenchmarkCycles PROC
    rdtsc                               ; EDX:EAX = time-stamp counter
    shl rdx, 32                         ; move the high half into place
    or rax, rdx                         ; RAX = (EDX << 32) | EAX
    ret
Morse_GetBenchmarkCycles ENDP
END
