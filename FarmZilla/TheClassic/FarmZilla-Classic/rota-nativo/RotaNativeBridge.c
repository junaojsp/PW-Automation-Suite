#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>

#define BRIDGE_MESSAGE (WM_APP + 0x5A7)
#define BRIDGE_MAGIC 0x52415631u
/* PW The Classic (elementclient.exe 39d81c21...): structure offsets differ from the Avalon client this bridge came from;
   each one is mapped by disassembly of that client (see TheClassic/PROFILE.md). Avalon values in comments. */
#define RUN_HOST 0x28        /* Avalon 0x34 */
#define RUN_UI 0x10          /* 0x1C */
#define RUN_QUEUE 0x68       /* 0x74 */
#define UI_TASK_CONFIRM 0xD54 /* 0x6B0 */
#define UI_TASK_HINT 0xD58   /* 0x6B4 */
#define CONFIRM_LABELS 0x250 /* 0x258 */
#define CONFIRM_COUNT 0x26C  /* 0x274 */
#define AWARD_CONTROLS 0x1B4 /* 0x1BC */
#define AWARD_FLAGS 0x254    /* 0x25C */
#define HOST_MACRO 0x1418    /* 0x1D3C */
typedef struct Request {
    DWORD magic, version, operation, pid, thread, nonce;
    volatile LONG state;
    DWORD executed_thread, error;
    float x, y, z;
    DWORD reserved;
    /* FarmZilla 0.50.0 (version 2): the game's addresses come from FarmZilla's profile (validated hash or complete byte-signature
       profile), in the fixed order of GameProfile.NativeOrder, plus the image size the profile was resolved for. */
    DWORD image_size;
    DWORD rva[24];
} Request;
enum {R_ROTA_ROOT,R_ROOT,R_TRACE,R_TASK_LOOKUP,R_HINT_COMMAND,R_CONFIRM_REFRESH,R_CONFIRM_COMMAND,R_AWARD_SELECT,R_AWARD_CONFIRM,
      R_POSTER,R_KEY_MOVE,R_POLICY_GET,R_POLICY_CHANGE,R_POLICY_STOP,R_AUTOKILL_NAME,R_EMPTY_STRING,R_NAVIGATE,R_COUNT};
static DWORD image_size;

static int readable(const void *p, size_t size) {
    MEMORY_BASIC_INFORMATION region;
    if (!p || VirtualQuery(p, &region, sizeof(region)) != sizeof(region)) return 0;
    uintptr_t begin=(uintptr_t)region.BaseAddress,address=(uintptr_t)p;
    return region.State==MEM_COMMIT && !(region.Protect&(PAGE_GUARD|PAGE_NOACCESS)) && region.RegionSize>=size && address>=begin && address-begin<=region.RegionSize-size;
}
static DWORD u32(uintptr_t address) { return readable((void *)address,4)?*(DWORD *)address:0; }
static int writable(Request *r) {
    MEMORY_BASIC_INFORMATION region;
    if (!r || VirtualQuery(r, &region, sizeof(region)) != sizeof(region)) return 0;
    uintptr_t begin = (uintptr_t)region.BaseAddress, address = (uintptr_t)r;
    if (region.State != MEM_COMMIT || (region.Protect & (PAGE_GUARD | PAGE_NOACCESS))) return 0;
    DWORD protection = region.Protect & 0xFF;
    if (protection != PAGE_READWRITE && protection != PAGE_EXECUTE_READWRITE && protection != PAGE_WRITECOPY) return 0;
    return region.RegionSize >= sizeof(Request) && address >= begin && address - begin <= region.RegionSize - sizeof(Request);
}

/* Every address must fall inside the image the profile was resolved for (header page excluded). */
static int table_ok(const Request *r) {
    if (r->image_size < 0x10000 || r->image_size > 0x10000000) return 0;
    for (unsigned i = 0; i < R_COUNT; ++i) if (r->rva[i] < 0x1000 || r->rva[i] >= r->image_size) return 0;
    return 1;
}
static BYTE *game_base(void) {
    WCHAR path[MAX_PATH];DWORD length=GetModuleFileNameW(NULL,path,MAX_PATH);if(!length||length>=MAX_PATH)return NULL;
    WCHAR *name=path;for(WCHAR *p=path;*p;++p)if(*p==L'\\'||*p==L'/')name=p+1;
    if(lstrcmpiW(name,L"ElementClient.exe"))return NULL;
    BYTE *base=(BYTE *)GetModuleHandleW(NULL);IMAGE_DOS_HEADER *dos=(IMAGE_DOS_HEADER *)base;
    IMAGE_NT_HEADERS *pe=(IMAGE_NT_HEADERS *)(base+dos->e_lfanew);
    return dos->e_magic==IMAGE_DOS_SIGNATURE&&pe->Signature==IMAGE_NT_SIGNATURE&&image_size>=0x10000&&pe->OptionalHeader.SizeOfImage==image_size?base:NULL;
}
static int native_type(uintptr_t object,BYTE *base,const char *expected) {
    uintptr_t vt=u32(object),begin=(uintptr_t)base,end=begin+image_size;
    if(vt<begin+4||vt>=end)return 0;
    uintptr_t col=u32(vt-4),desc=u32(col+12);
    return desc>=begin&&desc+96<end&&!lstrcmpA((char *)(desc+8),expected);
}
static DWORD collect(Request *r) {
    BYTE *base=game_base();if(!base)return ERROR_BAD_EXE_FORMAT;
    if((r->reserved&0xC0000000u)!=0xC0000000u)return ERROR_INVALID_PARAMETER;
    uintptr_t root=u32((uintptr_t)base+r->rva[R_ROTA_ROOT]),actor=u32(root+RUN_HOST);
    if(!native_type(actor,base,".?AVCECHostPlayer@@"))return ERROR_INVALID_DATA;
    BYTE *function=base+r->rva[R_TRACE];const BYTE signature[]={0x51,0x53,0x55,0x56,0x57,0x8B,0xF9};
    for(unsigned i=0;i<sizeof(signature);++i)if(function[i]!=signature[i])return ERROR_REVISION_MISMATCH;
    DWORD accepted,key=r->reserved;
    /* Native host TraceMatter(instance, gathering=true), callee-cleans 8 bytes. */
    __asm__ __volatile__("pushl $1\n\tpushl %2\n\tmovl %1, %%ecx\n\tcall *%3\n\tmovzbl %%al, %%eax"
        : "=a"(accepted) : "r"(actor),"r"(key),"r"(function) : "ecx","edx","cc","memory");
    return accepted?ERROR_SUCCESS:ERROR_REQUEST_ABORTED;
}
typedef void (__attribute__((thiscall)) *VoidMethod)(void *);
typedef void (__attribute__((thiscall)) *CommandMethod)(void *,const char *);
typedef void *(__attribute__((thiscall)) *GetDialogMethod)(void *,const char *);

static uintptr_t quest_dialog(BYTE *base,const Request *r) {
    uintptr_t root=u32((uintptr_t)base+r->rva[R_ROTA_ROOT]),ui=u32(root+RUN_UI),gameui=u32(ui+8),dialog=u32(gameui+UI_TASK_CONFIRM);
    return native_type(dialog,base,".?AVCDlgTaskConfirm@@")?dialog:0;
}
static void row_name(char *out,const char *prefix,unsigned number) {
    while(*prefix)*out++=*prefix++;
    if(number>=10)*out++=(char)('0'+number/10);
    *out++=(char)('0'+number%10);*out=0;
}
static int material_text(uintptr_t text) {
    if(!readable((void *)text,128))return 0;
    const WCHAR *s=(const WCHAR *)text,*expected=L"Material";
    for(unsigned at=0;at<48&&s[at];++at){unsigned n=0;while(n<8&&s[at+n]==expected[n])++n;if(n==8)return 1;}
    return 0;
}
static int material_label(uintptr_t label,BYTE *base,DWORD task,const Request *r) {
    if(material_text(u32(label+0xC0)))return 1;
    if(!task)return 0;
    uintptr_t game=u32((uintptr_t)base+r->rva[R_ROOT]),manager=u32(game+0x228);
    if(!readable((void *)manager,0x24))return 0;
    typedef uintptr_t (__attribute__((thiscall)) *TaskLookup)(void *,DWORD);
    uintptr_t entry=((TaskLookup)(base+r->rva[R_TASK_LOOKUP]))((void *)manager,task);
    return readable((void *)entry,0x448)&&u32(entry+4)==task&&material_text(u32(entry+0x444));
}
static DWORD prepare_material_reward(Request *r) {
    BYTE *base=game_base();if(!base)return ERROR_BAD_EXE_FORMAT;
    uintptr_t dialog=quest_dialog(base,r);if(!dialog)return ERROR_INVALID_DATA;
    if(r->operation==3){uintptr_t root=u32((uintptr_t)base+r->rva[R_ROTA_ROOT]),ui=u32(root+RUN_UI),gameui=u32(ui+8),hint=u32(gameui+UI_TASK_HINT);if(!native_type(hint,base,".?AVCDlgTaskHintPop@@"))return ERROR_INVALID_DATA;((CommandMethod)(base+r->rva[R_HINT_COMMAND]))((void *)hint,"Btn_ShowTasksToConfirm");}
    ((VoidMethod)(base+r->rva[R_CONFIRM_REFRESH]))((void *)dialog);
    DWORD count=u32(dialog+CONFIRM_COUNT),labels=u32(dialog+CONFIRM_LABELS),row=0,quest=0;
    if(count>18)return ERROR_INVALID_DATA;
    for(DWORD i=0;i<count;++i){uintptr_t label=u32(labels+i*4);DWORD task=u32(label+0xF4);/* IDs observed in controlled Coal, Willow and Coal Core captures; text was empty. *//* FarmZilla 0.28.0: with an expected task (material + 58456 for the 3074..3092 family), only that exact row; without one, the known ids or the "Material" text as before. */
        if(native_type(label,base,".?AVAUILabel@@")&&(r->reserved?task==r->reserved:(task==61546||task==61532||task==61547||material_label(label,base,task,r)))){if(row)return ERROR_INVALID_DATA;row=i+1;quest=task;}}
    if(!row||!quest)return ERROR_NO_MORE_ITEMS;
    char command[20];row_name(command,"Btn_Done",row);
    if(r->operation!=5)((CommandMethod)(base+r->rva[R_CONFIRM_COMMAND]))((void *)dialog,command);
    r->reserved=quest;return ERROR_SUCCESS;
}
static uintptr_t control(uintptr_t dialog,const char *name) {
    uintptr_t map=dialog+AWARD_CONTROLS;DWORD modulus=u32(map+0x18),array=u32(map+0xC),hash=0;if(!modulus)modulus=u32(map+8);
    if(!modulus||modulus>65536||!array)return 0;
    for(const unsigned char *p=(const unsigned char *)name;*p;++p)hash=hash*31+*p;
    uintptr_t node=u32(array+(hash%modulus)*4);
    for(unsigned n=0;node&&n<128;++n){uintptr_t key=u32(node+8);if(readable((void *)key,32)&&!lstrcmpA((const char *)key,name))return u32(node+4);node=u32(node);}
    return 0;
}
static DWORD confirm_second_reward(Request *r) {
    BYTE *base=game_base();if(!base)return ERROR_BAD_EXE_FORMAT;
    uintptr_t quest=quest_dialog(base,r),manager=quest?u32(quest+0x34):0;if(!quest||!manager)return ERROR_INVALID_DATA;
    uintptr_t getDialog=u32(u32(manager)+0x34);
    if(getDialog<(uintptr_t)base||getDialog>=(uintptr_t)base+image_size)return ERROR_INVALID_DATA;
    uintptr_t dialog=(uintptr_t)((GetDialogMethod)getDialog)((void *)manager,"Win_Award");
    if(!native_type(dialog,base,".?AVCDlgAward@@")||u32(dialog+0xFC)!=r->reserved)return ERROR_NO_MORE_ITEMS;
    if(!readable((void *)(dialog+AWARD_FLAGS),2)||*(BYTE *)(dialog+AWARD_FLAGS)||*(BYTE *)(dialog+AWARD_FLAGS+1))return ERROR_NOT_SUPPORTED;
    /* Coque (resource 3092, quest 61548) awards the desired dark item in the first slot.
       Keep the previously verified second-slot selection for every other material. */
    unsigned award=(r->reserved==61548u)?1u:2u;
    char wanted_name[20];row_name(wanted_name,"Rdo_Award",award);
    uintptr_t wanted=control(dialog,wanted_name);
    if(!native_type(wanted,base,".?AVAUIRadioButton@@")||!readable((void *)wanted,0x132)||!*(BYTE *)(wanted+0x7C))return ERROR_NO_MORE_ITEMS;
    for(unsigned i=1;i<=12;++i){char name[20];row_name(name,"Rdo_Award",i);uintptr_t radio=control(dialog,name);if(radio&&native_type(radio,base,".?AVAUIRadioButton@@")&&readable((void *)radio,0x132))*(BYTE *)(radio+0x131)=(i==award);}
    ((CommandMethod)(base+r->rva[R_AWARD_SELECT]))((void *)dialog,"select");
    ((CommandMethod)(base+r->rva[R_AWARD_CONFIRM]))((void *)dialog,"confirm");
    return ERROR_SUCCESS;
}

/* FarmZilla 0.27.0: the game's own auto-move messages (0x14A) with height control, mapped by disassembly on 02/11/2026.
   type 0 = fly straight to (x,z) with height control on (what a map click does); type 1 = height target in game
   units (Y/10, e.g. 60), which also takes off when not flying; type 2 = height control off. The host handler
   (RVA 0x3D7000) runs them on the game thread when it drains its queue; here they are only queued, on the window thread. */
static DWORD post_automove(Request *r) {
    BYTE *base=game_base();if(!base)return ERROR_BAD_EXE_FORMAT;
    BYTE *poster=base+r->rva[R_POSTER];const BYTE expected[]={0x55,0x8B,0xEC,0x6A,0xFF,0x68};
    for(unsigned i=0;i<sizeof(expected);++i)if(poster[i]!=expected[i])return ERROR_REVISION_MISMATCH;
    uintptr_t game=u32((uintptr_t)base+r->rva[R_ROOT]),run=u32(game+0x1C),queue=u32(run+RUN_QUEUE);
    if(!game||!run||!queue||!readable((void *)queue,16))return ERROR_INVALID_DATA;
    DWORD type=r->reserved;volatile DWORD message[10]={0x14A,0,0,type,0,0,0,0,0,0};
    if(type==0){
        if(!(r->x>=-100000&&r->x<=100000&&r->z>=-100000&&r->z<=100000))return ERROR_INVALID_PARAMETER;
        message[4]=(DWORD)(int)r->x;message[5]=(DWORD)(int)r->z;message[6]=r->y!=0?1:0;
    }else if(type==1){
        if(!(r->y>=1&&r->y<=1000))return ERROR_INVALID_PARAMETER;
        message[4]=(DWORD)(int)r->y;message[5]=0;
    }else if(type!=2)return ERROR_INVALID_PARAMETER;
    uintptr_t target=queue+8;void *pointer=(void *)message;
    /* thiscall poster(this=queue+8, message*), callee cleans 4 bytes; same call shape as RVA 0x4DFF80. */
    __asm__ __volatile__("pushl %1\n\tmovl %0, %%ecx\n\tcall *%2"
        : : "r"(target),"r"(pointer),"r"(poster) : "eax","ecx","edx","cc","memory");
    return ERROR_SUCCESS;
}

/* FarmZilla 0.45.6 (operation 7): what Space / Z do. The Avalon host keyboard-move method (RVA 0x3D73C0) takes a command
   whose +0x0C is the key: 8 = up (sets 0x10 in host+0xB78), 9 = down (0x20). The Classic has no separate method: the same
   command arrives as message 0xDD through CECHostPlayer::ProcessMessage (key in dwParam1, +0x0C; bits in host+0xAF8). While flying it
   starts the keyboard move work (work type 1, mode 3: no destination), which is what applies the vertical direction; the
   direction itself is cleared by the game every frame and held by FarmZilla from outside while it climbs/descends. */
static DWORD key_move(Request *r) {
    BYTE *base=game_base();if(!base)return ERROR_BAD_EXE_FORMAT;
    DWORD key=r->reserved;if(key!=8&&key!=9)return ERROR_INVALID_PARAMETER;
    BYTE *function=base+r->rva[R_KEY_MOVE];const BYTE expected[]={0x53,0x8B,0xDC,0x83,0xEC,0x08};
    for(unsigned i=0;i<sizeof(expected);++i)if(function[i]!=expected[i])return ERROR_REVISION_MISMATCH;
    uintptr_t root=u32((uintptr_t)base+r->rva[R_ROTA_ROOT]),actor=u32(root+RUN_HOST);
    if(!native_type(actor,base,".?AVCECHostPlayer@@"))return ERROR_INVALID_DATA;
    volatile DWORD command[8]={0xDD,0,0,key,0,0,0,0};void *pointer=(void *)command;
    /* thiscall host->ProcessMessage(message*), callee cleans 4 bytes. */
    __asm__ __volatile__("pushl %1\n\tmovl %0, %%ecx\n\tcall *%2"
        : : "r"(actor),"r"(pointer),"r"(function) : "eax","ecx","edx","cc","memory");
    return ERROR_SUCCESS;
}

/* FarmZilla 0.46.0 (operation 8): the game's own auto-cultivation ("alto cultivo", F12), mapped by disassembly on
   04/11/2026 from the auto-policy dialog button (VA 0x5071F0): the CECAutoPolicy singleton (getter RVA 0x313620) runs
   when its policy name (+0x28) is not the empty string (RVA 0x11BA970). Off = stop (RVA 0x3136E0). On = change policy
   (RVA 0x313790) to "AutoKillMonster", clear +0x38, drop the character's running macro and take the current position
   as the policy's home point, like the button does. reserved: 1 = on, 0 = off. */
static DWORD auto_policy(Request *r) {
    BYTE *base=game_base();if(!base)return ERROR_BAD_EXE_FORMAT;
    DWORD on=r->reserved;if(on>1)return ERROR_INVALID_PARAMETER;
    BYTE *getter=base+r->rva[R_POLICY_GET],*change=base+r->rva[R_POLICY_CHANGE],*stop=base+r->rva[R_POLICY_STOP];
    const BYTE g[]={0x64,0xA1,0x2C,0x00,0x00,0x00,0x8B,0x08},c[]={0x55,0x8B,0xEC,0x6A,0xFF,0x68},t[]={0x55,0x8B,0xEC,0x83,0xE4,0xF8,0x51,0x53,0x56,0x57};
    for(unsigned i=0;i<sizeof(g);++i)if(getter[i]!=g[i])return ERROR_REVISION_MISMATCH;
    for(unsigned i=0;i<sizeof(c);++i)if(change[i]!=c[i])return ERROR_REVISION_MISMATCH;
    for(unsigned i=0;i<sizeof(t);++i)if(stop[i]!=t[i])return ERROR_REVISION_MISMATCH;
    const char *policy=(const char *)(base+r->rva[R_AUTOKILL_NAME]);if(lstrcmpA(policy,"AutoKillMonster"))return ERROR_REVISION_MISMATCH;
    uintptr_t inst;__asm__ __volatile__("call *%1":"=a"(inst):"r"(getter):"ecx","edx","cc","memory");
    if(!native_type(inst,base,".?AVCECAutoPolicy@@")||!readable((void *)inst,0x40))return ERROR_INVALID_DATA;
    uintptr_t empty=u32((uintptr_t)base+r->rva[R_EMPTY_STRING]);if(!empty)return ERROR_INVALID_DATA;int running=u32(inst+0x28)!=empty;
    r->y=running?1.0f:0.0f;
    if(!on){
        if(running)__asm__ __volatile__("movl %0, %%ecx\n\tcall *%1"::"r"(inst),"r"(stop):"eax","ecx","edx","cc","memory");
        return ERROR_SUCCESS;
    }
    if(running)return ERROR_SUCCESS;
    /* thiscall ChangePolicy(name), callee cleans 4 bytes. */
    __asm__ __volatile__("pushl %1\n\tmovl %0, %%ecx\n\tcall *%2"::"r"(inst),"r"(policy),"r"(change):"eax","ecx","edx","cc","memory");
    *(volatile DWORD *)(inst+0x38)=0;
    uintptr_t action=u32(inst+0x24);
    if(action&&readable((void *)action,0x24)){
        uintptr_t host=u32(action+4);
        if(host&&native_type(host,base,".?AVCECHostPlayer@@")&&readable((void *)(host+HOST_MACRO),4)){
            uintptr_t macro=u32(host+HOST_MACRO);
            if(macro&&readable((void *)macro,4)){uintptr_t vt=u32(macro),destroy=u32(vt);
                if(destroy){__asm__ __volatile__("pushl $1\n\tmovl %0, %%ecx\n\tcall *%1"::"r"(macro),"r"(destroy):"eax","ecx","edx","cc","memory");}
                *(volatile DWORD *)(host+HOST_MACRO)=0;}
            volatile float *from=(volatile float *)(host+0x3C),*to=(volatile float *)(action+0x18);to[0]=from[0];to[1]=from[1];to[2]=from[2];
        }
    }
    return u32(inst+0x28)!=empty?ERROR_SUCCESS:ERROR_REQUEST_ABORTED;
}

static DWORD navigate(Request *r) {
    if (!(r->x >= -100000 && r->x <= 100000 && r->y >= 0 && r->y <= 10000 && r->z >= -100000 && r->z <= 100000)) return ERROR_INVALID_PARAMETER;
    WCHAR path[MAX_PATH];
    if (!GetModuleFileNameW(NULL, path, MAX_PATH)) return GetLastError();
    WCHAR *name = path;
    for (WCHAR *p = path; *p; ++p) if (*p == L'\\' || *p == L'/') name = p + 1;
    if (lstrcmpiW(name, L"ElementClient.exe")) return ERROR_BAD_EXE_FORMAT;
    BYTE *base = (BYTE *)GetModuleHandleW(NULL);
    IMAGE_DOS_HEADER *dos = (IMAGE_DOS_HEADER *)base;
    IMAGE_NT_HEADERS *pe = (IMAGE_NT_HEADERS *)(base + dos->e_lfanew);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE || pe->Signature != IMAGE_NT_SIGNATURE || image_size < 0x10000 || pe->OptionalHeader.SizeOfImage != image_size) return ERROR_BAD_EXE_FORMAT;
    BYTE *function = base + r->rva[R_NAVIGATE];
    const BYTE expected[] = {0x55,0x8B,0xEC,0x6A,0xFF,0x68};
    for (unsigned i = 0; i < sizeof(expected); ++i) if (function[i] != expected[i]) return ERROR_REVISION_MISMATCH;
    float *position = &r->x;
    /* Observed ABI: ECX = vector, EDX = mode 0, caller-cleans stack argument 0.
       Run only here, on the window thread, never on a remote worker thread. */
    __asm__ __volatile__(
        "pushl $0\n\t"
        "movl %0, %%ecx\n\t"
        "xorl %%edx, %%edx\n\t"
        "call *%1\n\t"
        "addl $4, %%esp\n\t"
        : : "r"(position), "r"(function) : "eax", "ecx", "edx", "cc", "memory");
    return ERROR_SUCCESS;
}

__declspec(dllexport) LRESULT CALLBACK NativeHookProc(int code, WPARAM removed, LPARAM payload) {
    if (code >= 0 && removed == PM_REMOVE && payload) {
        MSG *message = (MSG *)payload;
        if (message->message == BRIDGE_MESSAGE) {
            Request *r = (Request *)message->lParam;
            if (writable(r) && r->magic == BRIDGE_MAGIC && r->version == 2 && r->nonce == (DWORD)message->wParam && r->pid == GetCurrentProcessId() && r->thread == GetCurrentThreadId()) {
                message->message = WM_NULL;
                message->wParam = 0;
                message->lParam = 0;
                if (InterlockedCompareExchange(&r->state, 1, 0) == 0) {
                    r->executed_thread = GetCurrentThreadId();
                    image_size = r->image_size;
                    r->error = r->operation == 0 ? 0 : !table_ok(r) ? ERROR_INVALID_PARAMETER : r->operation == 1 ? navigate(r) : r->operation==2?collect(r):(r->operation==3||r->operation==5)?prepare_material_reward(r):r->operation==4?confirm_second_reward(r):r->operation==6?post_automove(r):r->operation==7?key_move(r):r->operation==8?auto_policy(r):ERROR_INVALID_PARAMETER;
                    InterlockedExchange(&r->state, r->error ? -1 : 2);
                }
            }
        }
    }
    return CallNextHookEx(NULL, code, removed, payload);
}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved) {
    (void)module; (void)reason; (void)reserved;
    return TRUE;
}
