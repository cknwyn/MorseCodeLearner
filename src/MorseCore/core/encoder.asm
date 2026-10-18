.code
EXTERN MorseAsciiTable:WORD
PUBLIC Morse_EncodeChar
Morse_EncodeChar PROC
    cmp ecx, 'a'
    jb @Lookup
    cmp ecx, 'z'
    ja @Lookup
    sub ecx, 20h
@Lookup:
    and ecx, 7Fh
    lea rdx, MorseAsciiTable
    movzx eax, word ptr [rdx + rcx * 2]
    ret
Morse_EncodeChar ENDP
PUBLIC Morse_EncodeString
Morse_EncodeString PROC
    push rbx
    push rsi
    push rdi
    mov rsi, rcx
    mov rdi, rdx
    mov r9d, r8d
    xor ebx, ebx
@Loop:
    test r9d, r9d
    jz @Done
    movzx ecx, byte ptr [rsi]
    test cl, cl
    jz @Done
    call Morse_EncodeChar
    mov word ptr [rdi], ax
    inc rsi
    add rdi, 2
    inc ebx
    dec r9d
    jmp @Loop
@Done:
    mov eax, ebx
    pop rdi
    pop rsi
    pop rbx
    ret
Morse_EncodeString ENDP
END