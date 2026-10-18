.code
PUBLIC Morse_GetBenchmarkCycles
Morse_GetBenchmarkCycles PROC
    rdtsc
    shl rdx, 32
    or rax, rdx
    ret
Morse_GetBenchmarkCycles ENDP
END