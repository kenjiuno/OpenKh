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
  trap_puti(sizeof(int), "int=");
  trap_puti(sizeof(char), "char=");
  trap_puti(sizeof(short), "short=");
  trap_puti(sizeof(float), "float=");
  trap_puti(sizeof(int *) * 10 + sizeof(char *), "ptr=");
  trap_puti(sizeof(x) * 10 + sizeof x, "var=");
  trap_puti(sizeof(a) * 100 + sizeof a / sizeof a[0], "arr=");
  trap_puti(sizeof(s) * 100 + sizeof(t), "chars=");
  trap_puti(sizeof(m) * 100 + sizeof(m[0]), "m=");
  trap_puti(sizeof(struct P) * 100 + sizeof(gp), "st=");
  trap_puti(sizeof(struct N) * 1000 + sizeof(n) * 10 + sizeof(*pn) / 4, "n=");
  trap_puti(sizeof(ns) / sizeof(ns[0]), "ns=");
  trap_puti(sizeof(garr) * 100 + sizeof(gbuf), "glob=");
  trap_puti(count(a), "param=");
  trap_puti(sizeof(f) * 100 + sizeof(c) * 10 + sizeof(h), "fc=");
  trap_puti(sizeof(x + 1) * 10 + sizeof(c + 1), "expr=");
  x = 5;
  trap_puti(sizeof(x++) * 10 + x, "noeval=");
}
