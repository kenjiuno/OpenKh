// bdEmu e2e: sizeof of structs and variables in array lengths (locals, globals, struct members)
struct P { int x; int y; };
struct N { int id; char name[6]; struct P pos; };
int gp[sizeof(struct P) / 4];
char gbuf[sizeof(struct N)];
int base[5];
int gcopy[sizeof(base) / sizeof(base[0])];
struct Q { int v[sizeof(struct P)]; char t[sizeof(int) * 2]; };
void main() {
  int a[sizeof(struct N) / 4];
  struct P ps[sizeof(gp) / sizeof(gp[0]) + 1];
  char buf[sizeof(struct P) * 2];
  int own[sizeof(a) / 4];
  struct Q q;
  int i;
  for (i = 0; i < 5; i++) { a[i] = i + 1; }
  gp[1] = 8;
  gbuf[19] = 'z';
  gcopy[4] = 6;
  ps[2].y = 9;
  buf[15] = 'k';
  own[4] = 3;
  q.v[7] = 5;
  q.t[7] = 'm';
  trap_puti("a=", sizeof(a) * 100 + a[4]);
  trap_puti("gp=", sizeof(gp) * 10 + gp[1]);
  trap_puti("gbuf=", sizeof(gbuf) * 1000 + gbuf[19]);
  trap_puti("gcopy=", sizeof(gcopy) * 10 + gcopy[4]);
  trap_puti("ps=", sizeof(ps) * 10 + ps[2].y);
  trap_puti("buf=", sizeof(buf) * 1000 + buf[15]);
  trap_puti("own=", sizeof(own) * 10 + own[4]);
  trap_puti("q=", sizeof(q) * 1000 + q.v[7] * 100 + q.t[7]);
}
