// bdEmu e2e: function pointers hold the address of the code (pc): they are made, kept, passed and compared, but not called.
typedef void (*Hook)(int);
struct Hooks { int id; Hook on_init; Hook on_exit; };
void one(int a) { }
void two(int a) { }
void three(int a) { }
Hook gsingle = one;
Hook gtable[3] = { one, two, three };
int count;

int is_one(Hook h) { return h == one; }
Hook pick(int which) { if (which == 0) { return one; } if (which == 1) { return two; } return 0; }
void store(Hook *where, Hook what) { *where = what; }

void main() {
  Hook p = one;
  Hook ts[3];
  struct Hooks hs;
  Hook *pp = &p;
  int i;
  trap_puti((p == one) * 100 + (p == two) * 10 + (p != 0), "eq=");
  p = two;
  trap_puti((p == two) * 100 + (p == one) * 10 + is_one(p), "re=");
  trap_puti(is_one(one) * 10 + is_one(three), "arg=");
  trap_puti((pick(0) == one) * 100 + (pick(1) == two) * 10 + (pick(2) == 0), "ret=");
  trap_puti((gsingle == one) * 100 + (gtable[1] == two) * 10 + (gtable[2] == three), "global=");
  for (i = 0; i < 3; i++) { if (gtable[i] != 0) { count++; } }
  trap_puti(count, "count=");
  ts[0] = one; ts[1] = two; ts[2] = one;
  trap_puti((ts[0] == ts[2]) * 100 + (ts[0] == ts[1]) * 10 + (ts[1] == two), "arr=");
  hs.id = 7; hs.on_init = three; hs.on_exit = 0;
  trap_puti((hs.on_init == three) * 100 + (hs.on_exit == 0) * 10 + hs.id, "member=");
  store(pp, three);
  trap_puti((p == three) * 10 + (*pp == three), "ptr=");
  trap_puti((*pp == p) * 10 + (**pp == p), "deref=");
  trap_puti((&one == one) * 10 + (&two != one), "addr=");
  p = 0;
  trap_puti((p == 0) * 10 + !p, "null=");
  trap_puti(sizeof(p) * 100 + sizeof(Hook) * 10 + sizeof(hs) / 4, "sz=");
}
