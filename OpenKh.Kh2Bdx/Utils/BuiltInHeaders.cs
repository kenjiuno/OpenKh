using System;
using System.Collections.Generic;

namespace OpenKh.Kh2Bdx.Utils
{
    /// <summary>
    /// The headers that come with the compiler (`#include &lt;kh2/traps.h&gt;`). The include provider of the caller is asked first,
    /// so a header of the same name in an include directory takes the place of the built-in one.
    /// </summary>
    public sealed class BuiltInIncludeProvider : IIncludeProvider
    {
        private const string Prefix = "builtin:";
        private readonly IIncludeProvider? _user;

        public BuiltInIncludeProvider(IIncludeProvider? user)
        {
            _user = user is BuiltInIncludeProvider inner ? inner._user : user;
        }

        public string? Find(string name, bool angled, string includingId)
        {
            return _user?.Find(name, angled, includingId)
                ?? (Files.ContainsKey(name) ? Prefix + name : null);
        }

        public string Load(string id) =>
            id.StartsWith(Prefix, StringComparison.Ordinal)
                ? Files[id.Substring(Prefix.Length)]
                : _user!.Load(id);

        private static readonly Dictionary<string, string> Files = new()
        {
            ["kh2/traps.h"] = TrapsHeader,
        };

        /// <summary>
        /// The types of some traps, from the host (EBOOT.ELF of the PS3 version) and from the data. Only those that were checked are here.
        /// The number of arguments and whether they return a value are checked against the trap table when the header is read.
        /// </summary>
        private const string TrapsHeader = @"#pragma once
/* kh2/traps.h: the types of some traps. */

struct FVector { float x; float y; float z; float w; };

/* memory: the host's heap. The pointers are real addresses of the host, there is no check. */
void *trap_memory_alloc(int size);
void trap_memory_free(void *p);
void *trap_teamwork_alloc(int id, int size);   /* the work memory of the allocator `id` (shared) */
void *trap_worldwork_get(void);                /* the host's WORLDWORK */

/* system */
float trap_frametime(void);
void func_system_set_game_speed(float scale);
void trap_signal_call(int signal, int arg);
void trap_signal_hook(int signal, int arg);
void trap_jigsaw_get(int picture, int piece);

/*
 * math: the vector that these return is in a buffer of the host (static, shared by all threads). It is overwritten by the next
 * call: copy it at once. trap_vector_normalize and trap_vector_homing change the vector that is given.
 */
struct FVector *trap_vector_add(struct FVector *a, struct FVector *b);
float trap_vector_normalize(struct FVector *v);
void trap_vector_homing(struct FVector *dir, struct FVector *target, float limit);
int trap_bg_cross_pos(struct FVector *from, struct FVector *to, struct FVector *cross, int type);
int trap_obj_cmp(void *a, void *b);            /* the difference of the two objects (0 if one is null) */

/*
 * threads: `entry` is the address of the code of a function (its pc), which is what a function pointer is. It is started in the
 * VM of `obj` with `arg`, in the group `group` (the threads of a group that is not 0 are stopped first). It returns the thread.
 */
void *trap_obj_thread_start(void *obj, int group, void (*entry)(int), int flag, int arg);
";
    }
}
