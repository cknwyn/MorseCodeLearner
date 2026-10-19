; ============================================================================
; dllmain.asm - DLL entry-point stub
;
; MorseCore.dll is linked with /NOENTRY /NODEFAULTLIB (see build-native.ps1):
; there is no C/C++ runtime and no per-process initialisation to perform, so
; Windows never needs to call this routine. It is kept as a documented,
; always-succeeding DllMain in case an explicit entry point is ever required
; (link with /ENTRY:DllMain).
; ============================================================================
.code
PUBLIC DllMain
DllMain PROC
    mov eax, 1                  ; Return TRUE (successful DLL initialization)
    ret
DllMain ENDP
END
