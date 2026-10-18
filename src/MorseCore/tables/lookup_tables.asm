.const
ALIGN 16
PUBLIC MorseAsciiTable
PUBLIC DichotomicMorseTable
; Format per entry: DB Count, Pattern (0=Dit, 1=Dah, MSB-first)
MorseAsciiTable LABEL WORD
    ; 0x00 - 0x1F Control Characters (32 entries = 64 bytes)
    DB 64 DUP(0)
    ; 0x20 - 0x2B
    DB 0, 0                     ; ' ' (Space)
    DB 0, 0, 0, 0, 0, 0         ; '!' '"' '#'
    DB 0, 0, 0, 0, 0, 0, 0, 0   ; '$' '%' '&' '\''
    DB 0, 0, 0, 0, 0, 0, 0, 0   ; '(' ')' '*' '+'
    DB 6, 33h                   ; 0x2C: ',' = --..-- (110011b = 0x33)
    DB 0, 0                     ; 0x2D: '-'
    DB 6, 15h                   ; 0x2E: '.' = .-.-.- (010101b = 0x15)
    DB 0, 0                     ; 0x2F: '/'
    ; 0x30 - 0x39 (Digits '0' - '9')
    DB 5, 1Fh                   ; '0' = ----- (11111b = 0x1F)
    DB 5, 0Fh                   ; '1' = .---- (01111b = 0x0F)
    DB 5, 07h                   ; '2' = ..--- (00111b = 0x07)
    DB 5, 03h                   ; '3' = ...-- (00011b = 0x03)
    DB 5, 01h                   ; '4' = ....- (00001b = 0x01)
    DB 5, 00h                   ; '5' = ..... (00000b = 0x00)
    DB 5, 10h                   ; '6' = -.... (10000b = 0x10)
    DB 5, 18h                   ; '7' = --... (11000b = 0x18)
    DB 5, 1Ch                   ; '8' = ---.. (11100b = 0x1C)
    DB 5, 1Eh                   ; '9' = ----. (11110b = 0x1E)
    ; 0x3A - 0x40
    DB 0, 0, 0, 0, 0, 0, 0, 0   ; ':' ';' '<' '='
    DB 0, 0                     ; '>'
    DB 6, 0Ch                   ; 0x3F: '?' = ..--.. (001100b = 0x0C)
    DB 0, 0                     ; 0x40: '@'
    ; 0x41 - 0x5A (Uppercase Letters 'A' - 'Z')
    DB 2, 01h                   ; 'A' = .-    (01b)
    DB 4, 08h                   ; 'B' = -...  (1000b)
    DB 4, 0Ah                   ; 'C' = -.-.  (1010b)
    DB 3, 04h                   ; 'D' = -..   (100b)
    DB 1, 00h                   ; 'E' = .     (0b)
    DB 4, 02h                   ; 'F' = ..-.  (0010b)
    DB 3, 06h                   ; 'G' = --.   (110b)
    DB 4, 00h                   ; 'H' = ....  (0000b)
    DB 2, 00h                   ; 'I' = ..    (00b)
    DB 4, 07h                   ; 'J' = .---  (0111b)
    DB 3, 05h                   ; 'K' = -.-   (101b)
    DB 4, 04h                   ; 'L' = .-..  (0100b)
    DB 2, 03h                   ; 'M' = --    (11b)
    DB 2, 02h                   ; 'N' = -.    (10b)
    DB 3, 07h                   ; 'O' = ---   (111b)
    DB 4, 06h                   ; 'P' = .--.  (0110b)
    DB 4, 0Dh                   ; 'Q' = --.-  (1101b)
    DB 3, 02h                   ; 'R' = .-.   (010b)
    DB 3, 00h                   ; 'S' = ...   (000b)
    DB 1, 01h                   ; 'T' = -     (1b)
    DB 3, 01h                   ; 'U' = ..-   (001b)
    DB 4, 01h                   ; 'V' = ...-  (0001b)
    DB 3, 03h                   ; 'W' = .--   (011b)
    DB 4, 09h                   ; 'X' = -..-  (1001b)
    DB 4, 0Bh                   ; 'Y' = -.--  (1011b)
    DB 4, 0Ch                   ; 'Z' = --..  (1100b)
    ; Remainder of ASCII 128
    DB 74 DUP(0)
; Flattened Dichotomic Morse Tree (Index = (1 << Count) - 2 + Pattern)
DichotomicMorseTable LABEL BYTE
    ; Count = 1 (Indices 0..1: E, T)
    DB 'E', 'T'
    ; Count = 2 (Indices 2..5: I, A, N, M)
    DB 'I', 'A', 'N', 'M'
    ; Count = 3 (Indices 6..13: S, U, R, W, D, K, G, O)
    DB 'S', 'U', 'R', 'W', 'D', 'K', 'G', 'O'
    ; Count = 4 (Indices 14..29)
    DB 'H', 'V', 'F', 0, 'L', 0, 'P', 'J'
    DB 'B', 'X', 'C', 'Y', 'Z', 'Q', 0, 0
    ; Count = 5 (Indices 30..61) - Digits 5, 4, 3, 2, 1, 6, 7, 8, 9, 0
    DB '5', '4', 0, '3', 0, 0, 0, '2'
    DB 0, 0, 0, 0, 0, 0, 0, '1'
    DB '6', 0, 0, 0, 0, 0, 0, 0
    DB '7', 0, 0, 0, '8', 0, '9', '0'
    ; Reserved for Count = 6
    DB 64 DUP(0)
END