.const
ALIGN 16
PUBLIC MorseAsciiTable
PUBLIC DichotomicMorseTable
; Format per entry:
; Low Byte (AL)  = Element Count (1..6)
; High Byte (AH) = Bitmask (0 = Dit, 1 = Dah)
MorseAsciiTable LABEL WORD
    WORD 32 DUP(0)                      ; 0x00 - 0x1F Control
    WORD 0, 0, 0, 0, 0, 0, 0, 0         ; 0x20 - 0x27
    WORD 0, 0, 0, 0                     ; 0x28 - 0x2B
    WORD 0633h                          ; ',' = --..-- (Len 6, 0x33)
    WORD 0                              ; '-'
    WORD 0615h                          ; '.' = .-.-.- (Len 6, 0x15)
    WORD 0                              ; '/'
    WORD 051Fh                          ; '0' = ----- (Len 5, 0x1F)
    WORD 051Eh                          ; '1' = .---- (Len 5, 0x1E)
    WORD 051Ch                          ; '2' = ..--- (Len 5, 0x1C)
    WORD 0518h                          ; '3' = ...-- (Len 5, 0x18)
    WORD 0510h                          ; '4' = ....- (Len 5, 0x10)
    WORD 0500h                          ; '5' = ..... (Len 5, 0x00)
    WORD 0501h                          ; '6' = -.... (Len 5, 0x01)
    WORD 0503h                          ; '7' = --... (Len 5, 0x03)
    WORD 0507h                          ; '8' = ---.. (Len 5, 0x07)
    WORD 050Fh                          ; '9' = ----. (Len 5, 0x0F)
    WORD 0, 0, 0, 0, 0                  ; 0x3A - 0x3E
    WORD 060Ch                          ; '?' = ..--.. (Len 6, 0x0C)
    WORD 0                              ; '@'
    WORD 0202h                          ; 'A' = .-    (Len 2, 0x02)
    WORD 0401h                          ; 'B' = -...  (Len 4, 0x01)
    WORD 0405h                          ; 'C' = -.-.  (Len 4, 0x05)
    WORD 0301h                          ; 'D' = -..   (Len 3, 0x01)
    WORD 0100h                          ; 'E' = .     (Len 1, 0x00)
    WORD 0404h                          ; 'F' = ..-.  (Len 4, 0x04)
    WORD 0303h                          ; 'G' = --.   (Len 3, 0x03)
    WORD 0400h                          ; 'H' = ....  (Len 4, 0x00)
    WORD 0200h                          ; 'I' = ..    (Len 2, 0x00)
    WORD 040Eh                          ; 'J' = .---  (Len 4, 0x0E)
    WORD 0305h                          ; 'K' = -.-   (Len 3, 0x05)
    WORD 0402h                          ; 'L' = .-..  (Len 4, 0x02)
    WORD 0203h                          ; 'M' = --    (Len 2, 0x03)
    WORD 0201h                          ; 'N' = -.    (Len 2, 0x01)
    WORD 0307h                          ; 'O' = ---   (Len 3, 0x07)
    WORD 0406h                          ; 'P' = .--.  (Len 4, 0x06)
    WORD 040Bh                          ; 'Q' = --.-  (Len 4, 0x0B)
    WORD 0302h                          ; 'R' = .-.   (Len 3, 0x02)
    WORD 0300h                          ; 'S' = ...   (Len 3, 0x00)
    WORD 0101h                          ; 'T' = -     (Len 1, 0x01)
    WORD 0304h                          ; 'U' = ..-   (Len 3, 0x04)
    WORD 0408h                          ; 'V' = ...-  (Len 4, 0x08)
    WORD 0306h                          ; 'W' = .--   (Len 3, 0x06)
    WORD 0409h                          ; 'X' = -..-  (Len 4, 0x09)
    WORD 040Dh                          ; 'Y' = -.--  (Len 4, 0x0D)
    WORD 0403h                          ; 'Z' = --..  (Len 4, 0x03)
    WORD 37 DUP(0)                      ; 0x5B - 0x7F
; Flattened Dichotomic Morse Tree (O(1) Reverse Decoder)
DichotomicMorseTable LABEL BYTE
    DB 'E', 'T'                                 ; Count = 1
    DB 'I', 'A', 'N', 'M'                       ; Count = 2
    DB 'S', 'U', 'R', 'W', 'D', 'K', 'G', 'O'   ; Count = 3
    DB 'H', 'V', 'F', 0, 'L', 0, 'P', 'J'       ; Count = 4
    DB 'B', 'X', 'C', 'Y', 'Z', 'Q', 0, 0
    DB '5', '4', 0, '3', 0, 0, 0, '2'           ; Count = 5
    DB 0, 0, 0, 0, 0, 0, 0, '1'
    DB '6', 0, 0, 0, 0, 0, 0, 0
    DB '7', 0, 0, 0, '8', 0, '9', '0'
    DB 64 DUP(0)                                ; Reserved Count = 6
END