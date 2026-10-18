TOKEN_NONE          EQU 0
TOKEN_DIT           EQU 1
TOKEN_DAH           EQU 2
TOKEN_INVALID       EQU 3
GAP_CONTINUE        EQU 0
GAP_COMMIT_CHAR     EQU 1
GAP_COMMIT_SPACE    EQU 2

.code
PUBLIC Morse_QuantizePress
Morse_QuantizePress PROC
    mov r8, rdx
    shr r8, 2
    cmp rcx, r8
    jb @Debounce
    imul r9, rdx, 6
    cmp rcx, r9
    ja @OverExtended
    mov r8, rdx
    shl r8, 1
    cmp rcx, r8
    jae @IsDah
    mov eax, TOKEN_DIT
    ret
@IsDah:
    mov eax, TOKEN_DAH
    ret
@OverExtended:
    mov eax, TOKEN_INVALID
    ret
@Debounce:
    mov eax, TOKEN_NONE
    ret
Morse_QuantizePress ENDP
PUBLIC Morse_EvaluateGap
Morse_EvaluateGap PROC
    imul r8, rdx, 6
    cmp rcx, r8
    jae @WordGap
    imul r8, rdx, 5
    shr r8, 1
    cmp rcx, r8
    jae @CharGap
    mov eax, GAP_CONTINUE
    ret
@CharGap:
    mov eax, GAP_COMMIT_CHAR
    ret
@WordGap:
    mov eax, GAP_COMMIT_SPACE
    ret
Morse_EvaluateGap ENDP
END