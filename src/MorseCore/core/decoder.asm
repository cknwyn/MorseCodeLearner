; ============================================================================
; decoder.asm - Morse -> ASCII via a flattened dichotomic tree (O(1))
;
; The Morse binary tree is stored level by level in a contiguous byte array
; (DichotomicMorseTable). A character with N elements and pattern P (MSB-first,
; 0 = dit, 1 = dah) lives at:
;                 index = (2^N - 2) + P
; so decoding is one shift, one subtract, one add and one memory read.
; Six-element punctuation (. , ?) is handled by explicit comparisons.
;
; ABI: Microsoft x64. CL = element count, DL = pattern. Result in AL
;      (0 = not found). Leaf routine: no stack use, no non-volatile registers.
; Caller contract: for N = 1..5 the pattern must be < 2^N; the routine does not
;      range-check it (the application only passes patterns it built itself).
; ============================================================================
.data
.code
EXTERN DichotomicMorseTable:BYTE        ; flattened tree in the .const segment

; BYTE Morse_DecodeChar(BYTE count, BYTE pattern)    CL = count, DL = pattern
PUBLIC Morse_DecodeChar
Morse_DecodeChar PROC
    cmp cl, 1                           ; count 0 is invalid
    jb @NotFound
    cmp cl, 5
    ja @CheckPunctuation                ; 6 elements -> punctuation path
    mov r8d, 1
    shl r8d, cl                         ; R8D = 2^count
    sub r8d, 2                          ; R8D = 2^count - 2 (start of this level)
    movzx edx, dl                       ; zero-extend the pattern
    add r8d, edx                        ; R8D = flattened index
    lea rax, DichotomicMorseTable       ; table base
    movzx eax, byte ptr [rax + r8]      ; base + index -> ASCII (0 if empty slot)
    ret
@CheckPunctuation:
    cmp cl, 6                           ; only 6-element codes exist beyond 5
    jne @NotFound
    cmp dl, 15h                         ; .-.-.-  -> '.'
    je @IsPeriod
    cmp dl, 33h                         ; --..--  -> ','
    je @IsComma
    cmp dl, 0Ch                         ; ..--..  -> '?'
    je @IsQuestion
    jmp @NotFound
@IsPeriod:
    mov al, '.'
    ret
@IsComma:
    mov al, ','
    ret
@IsQuestion:
    mov al, '?'
    ret
@NotFound:
    xor eax, eax                        ; 0 = no such character
    ret
Morse_DecodeChar ENDP
END
