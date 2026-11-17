// bdEmu e2e: char arrays in structs (copy, by value), 2D char arrays, global string initializers, byte-wise algorithms
struct N { int id; char name[6]; };
char names[3][8] = { "ab", "cde", "f" };
char gmsg[12] = "global";
int len(char *s) { int n = 0; while (s[n]) { n++; } return n; }
int same(char *a, char *b) {
  int i = 0;
  while (a[i]) { if (a[i] != b[i]) { return 0; } i++; }
  return a[i] == b[i];
}
int sumbytes(struct N n) { return n.id + n.name[0] + n.name[1] + n.name[4]; }
void upper(char *s) { while (*s) { if (*s >= 97) { if (*s <= 122) { *s -= 32; } } s++; } }
void main() {
  struct N a;
  struct N b;
  char m[2][4];
  char loc[8] = "mix";
  int i;
  a.id = 7;
  for (i = 0; i < 5; i++) { a.name[i] = 'a' + i; }
  a.name[5] = 0;
  b = a;
  a.name[0] = 'Z';
  trap_puti("b0=", b.name[0]);
  trap_puti("a0=", a.name[0]);
  trap_puti("bl=", len(b.name));
  trap_puti("sb=", sumbytes(b));
  trap_puti("n1=", names[1][1]);
  trap_puti("n2l=", len(names[2]));
  trap_puti("nl=", len(names[0]) * 100 + len(names[1]) * 10 + len(names[2]));
  m[1][2] = 'q';
  m[0][3] = 'r';
  trap_puti("m=", m[1][2] * 1000 + m[0][3]);
  trap_puti("gm=", gmsg[5]);
  trap_puti("gl=", len(gmsg));
  trap_puti("same1=", same(gmsg, "global"));
  trap_puti("same2=", same(gmsg, "globe"));
  upper(loc);
  trap_puti("up=", loc[0] * 10000 + loc[1] * 100 + loc[2]);
  trap_puts(names[1]);
  trap_puts(loc);
  for (i = 0; i < 3; i++) { names[i][0] = 'A' + i; }
  trap_puti("nn=", names[0][0] * 10000 + names[1][0] * 100 + names[2][0]);
}
