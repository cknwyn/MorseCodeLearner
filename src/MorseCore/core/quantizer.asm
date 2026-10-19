; ============================================================================
; quantizer.asm - Single-switch timing quantizer (Subsystem 2)
;
; Classifies a physical key press (and the idle gap that follows) against the
; Morse unit T, where T = 1,200,000 / WPM microseconds. Everything is integer
; arithmetic on RCX (measured time) and RDX (T); there are no divisions, and
; the caller may use any consistent time unit.
;
;   Press   (Morse_QuantizePress)        Gap     (Morse_EvaluateGap)
;     t <  T/4        -> NONE (debounce)   g <  2.5 T  -> CONTINUE
;     T/4 <= t < 2T   -> DIT               2.5T <= g < 6T -> COMMIT_CHAR
;     2T  <= t <= 6T  -> DAH               g >= 6T    -> COMMIT_SPACE
;     t >  6T         -> INVALID (held too long)
;
; The thresholds sit between the ideal 1T / 3T / 7T ITU-R M.1677-1 lengths so
; that human timing error is tolerated. Example at 15 WPM (T = 80 ms): dit
; 20..159 ms, dah 160..480 ms, character gap >= 200 ms, word gap >= 480 ms.
;
; ABI: Microsoft x64. RCX = measured time, RDX = T. Result in EAX.
; Leaf routines using only volatile registers (RAX, RCX, RDX, R8, R9).
; ============================================================================
TOKEN_NONE          EQU 0
TOKEN_DIT           EQU 1
TOKEN_DAH           EQU 2
TOKEN_INVALID       EQU 3
GAP_CONTINUE        EQU 0
GAP_COMMIT_CHAR     EQU 1
GAP_COMMIT_SPACE    EQU 2

.code
; DWORD Morse_QuantizePress(ULONGLONG pressTime, ULONGLONG unitT)
PUBLIC Morse_QuantizePress
Morse_QuantizePress PROC
    mov r8, rdx
    shr r8, 2                           ; R8 = T/4 (debounce threshold)
    cmp rcx, r8
    jb @Debounce                        ; shorter than T/4 -> switch bounce
    imul r9, rdx, 6                     ; R9 = 6T (over-extended threshold)
    cmp rcx, r9
    ja @OverExtended                    ; longer than 6T -> accidental hold
    mov r8, rdx
    shl r8, 1                           ; R8 = 2T (dit/dah boundary)
    cmp rcx, r8
    jae @IsDah                          ; 2T <= t <= 6T
    mov eax, TOKEN_DIT                  ; T/4 <= t < 2T
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

; DWORD Morse_EvaluateGap(ULONGLONG idleTime, ULONGLONG unitT)
PUBLIC Morse_EvaluateGap
Morse_EvaluateGap PROC
    imul r8, rdx, 6                     ; R8 = 6T (word-gap threshold)
    cmp rcx, r8
    jae @WordGap
    imul r8, rdx, 5
    shr r8, 1                           ; R8 = 5T / 2 = 2.5T (character-gap threshold)
    cmp rcx, r8
    jae @CharGap
    mov eax, GAP_CONTINUE               ; still inside the character
    ret
@CharGap:
    mov eax, GAP_COMMIT_CHAR            ; character complete: decode and dispatch
    ret
@WordGap:
    mov eax, GAP_COMMIT_SPACE           ; word complete: also insert a space
    ret
Morse_EvaluateGap ENDP
END
