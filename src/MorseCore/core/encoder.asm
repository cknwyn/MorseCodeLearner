; ============================================================================
; encoder.asm - ASCII -> bit-packed Morse (Subsystem 1: O(1) lookup engine)
;
; Representation (one 16-bit word per character, see tables\lookup_tables.asm):
;     AL = element count N (1..6, 0 = character has no Morse mapping)
;     AH = pattern bits, MSB-first: bit N-1 is the first element, bit 0 the last
;          (0 = dit, 1 = dah).  Example 'A' (.-) -> N = 2, pattern = 01b.
;
; ABI: Microsoft x64 calling convention. Arguments in RCX, RDX, R8, R9;
;      result in RAX. RBX, RSI, RDI are non-volatile and are preserved below.
; ============================================================================
.code
EXTERN MorseAsciiTable:WORD             ; 128 x WORD table in the .const segment

; ----------------------------------------------------------------------------
; WORD Morse_EncodeChar(DWORD asciiChar)         ECX = ASCII code
;   Returns the packed word for the character (0 if unsupported).
;   Cost: a fixed number of instructions, one memory read -> O(1).
; ----------------------------------------------------------------------------
PUBLIC Morse_EncodeChar
Morse_EncodeChar PROC
    cmp ecx, 'a'                        ; below 'a'? nothing to fold
    jb @Lookup
    cmp ecx, 'z'                        ; above 'z'? nothing to fold
    ja @Lookup
    sub ecx, 20h                        ; fold lowercase to uppercase
@Lookup:
    and ecx, 7Fh                        ; clamp the index to 0..127 (table size)
    lea rdx, MorseAsciiTable            ; RDX = table base address
    movzx eax, word ptr [rdx + rcx * 2] ; base + index*2 -> packed word
    ret
Morse_EncodeChar ENDP

; ----------------------------------------------------------------------------
; DWORD Morse_EncodeString(const char *in, WORD *out, DWORD maxWords)
;   RCX = zero-terminated ASCII string, RDX = output word buffer,
;   R8D = capacity of the output buffer in words.
;   Encodes until NUL or maxWords is reached; returns the words written in EAX.
; Note: the nested call does not reserve the 32-byte shadow space because
;   Morse_EncodeChar is a leaf that never uses it; the stack stays 16-byte
;   aligned (return address + 3 pushes = 32 bytes).
; ----------------------------------------------------------------------------
PUBLIC Morse_EncodeString
Morse_EncodeString PROC
    push rbx                            ; preserve non-volatile registers
    push rsi
    push rdi
    mov rsi, rcx                        ; RSI = input cursor
    mov rdi, rdx                        ; RDI = output cursor
    mov r9d, r8d                        ; R9D = remaining capacity
    xor ebx, ebx                        ; EBX = words written
@Loop:
    test r9d, r9d                       ; buffer full?
    jz @Done
    movzx ecx, byte ptr [rsi]           ; next input byte
    test cl, cl                         ; NUL terminator?
    jz @Done
    call Morse_EncodeChar               ; AX = packed word
    mov word ptr [rdi], ax              ; store it
    inc rsi                             ; advance input by 1 byte
    add rdi, 2                          ; advance output by 1 word
    inc ebx                             ; count it
    dec r9d                             ; one less slot left
    jmp @Loop
@Done:
    mov eax, ebx                        ; return the count
    pop rdi                             ; restore non-volatile registers
    pop rsi
    pop rbx
    ret
Morse_EncodeString ENDP
END
