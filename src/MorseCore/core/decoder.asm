.data
.code
EXTERN DichotomicMorseTable:BYTE
PUBLIC Morse_DecodeChar
Morse_DecodeChar PROC
    cmp cl, 1
    jb @NotFound
    cmp cl, 5
    ja @CheckPunctuation
    mov r8d, 1
    shl r8d, cl
    sub r8d, 2
    movzx edx, dl
    add r8d, edx
    lea rax, DichotomicMorseTable
    movzx eax, byte ptr [rax + r8]
    ret
@CheckPunctuation:
    cmp cl, 6
    jne @NotFound
    cmp dl, 15h
    je @IsPeriod
    cmp dl, 33h
    je @IsComma
    cmp dl, 0Ch
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
    xor eax, eax
    ret
Morse_DecodeChar ENDP
END