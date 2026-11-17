// bdEmu e2e: sizeof of types, variables, arrays, structs and expressions (the operand is not evaluated)
struct P { int x; int y; };
struct N { int id; char name[6]; struct P pos; };
struct P gp;
int garr[7];
char gbuf[10];
int count(int *p) { return sizeof(p); }
void main() {
  int a[5];
  char s[13];
  char t[] = "hello";
  char m[2][8];
  int x;
  struct N n;
  struct N ns[3];
  struct N *pn = &n;
  float f;
  char c;
  short h;
  trap_puti("int=", sizeof(int));
  trap_puti("char=", sizeof(char));
  trap_puti("short=", sizeof(short));
  trap_puti("float=", sizeof(float));
  trap_puti("ptr=", sizeof(int *) * 10 + sizeof(char *));
  trap_puti("var=", sizeof(x) * 10 + sizeof x);
  trap_puti("arr=", sizeof(a) * 100 + sizeof a / sizeof a[0]);
  trap_puti("chars=", sizeof(s) * 100 + sizeof(t));
  trap_puti("m=", sizeof(m) * 100 + sizeof(m[0]));
  trap_puti("st=", sizeof(struct P) * 100 + sizeof(gp));
  trap_puti("n=", sizeof(struct N) * 1000 + sizeof(n) * 10 + sizeof(*pn) / 4);
  trap_puti("ns=", sizeof(ns) / sizeof(ns[0]));
  trap_puti("glob=", sizeof(garr) * 100 + sizeof(gbuf));
  trap_puti("param=", count(a));
  trap_puti("fc=", sizeof(f) * 100 + sizeof(c) * 10 + sizeof(h));
  trap_puti("expr=", sizeof(x + 1) * 10 + sizeof(c + 1));
  x = 5;
  trap_puti("noeval=", sizeof(x++) * 10 + x);
}
