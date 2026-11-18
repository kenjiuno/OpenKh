// bdEmu e2e: typedef (scalars, pointers, structs, anonymous structs and enums, arrays, typedefs of typedefs, local typedef, sizeof)
typedef int s32;
typedef unsigned short u16;
typedef char *cstr;
typedef struct P { int x; int y; } P;
typedef struct { int a; int b; } Pair, *PairPtr;
typedef enum { OFF, ON = 4, AUTO } Mode;
typedef int Row[3];
typedef Row Grid[2];
typedef P Points[2];
typedef P *PP;
typedef s32 Count;
s32 total;
Mode gmode = AUTO;
Row grow = { 1, 2, 3 };
P gp = { 5, 6 };
Pair gpair;
s32 sum(Row r, Count n) { s32 i; s32 s = 0; for (i = 0; i < n; i++) { s += r[i]; } return s; }
P make(s32 x, s32 y) { P p; p.x = x; p.y = y; return p; }
s32 area(P p) { return p.x * p.y; }
Mode flip(Mode m) { return m == ON ? OFF : ON; }
s32 slen(cstr s) { s32 n = 0; while (s[n]) { n++; } return n; }
void main() {
  Grid g;
  Points ps;
  PP pp;
  Pair pr;
  PairPtr prp = &pr;
  u16 h = 65535;
  Mode m = ON;
  s32 i, j;
  typedef int Local;
  Local lv = 11;
  total = 3;
  for (i = 0; i < 2; i++) { for (j = 0; j < 3; j++) { g[i][j] = i * 10 + j; } }
  ps[0] = make(2, 3);
  ps[1] = ps[0];
  ps[1].x = 7;
  pp = &ps[1];
  prp->a = 4; prp->b = 9;
  trap_puti(sum(grow, 3) * 100 + sum(g[1], 3), "sum=");
  trap_puti(area(ps[0]) * 100 + area(*pp), "area=");
  trap_puti(gp.x * 10 + gp.y, "gp=");
  trap_puti(pr.a * 10 + pr.b + gpair.a, "pair=");
  trap_puti(gmode * 100 + flip(m) * 10 + flip(OFF), "mode=");
  trap_puti(h, "h=");
  trap_puti(slen("abcd") + lv + total, "slen=");
  trap_puti(sizeof(Grid) * 100 + sizeof(P) * 10 + sizeof(Mode) + sizeof(Pair) + sizeof(u16), "sz=");
  trap_puti(g[1][2] * 10 + g[0][1], "g=");
}
