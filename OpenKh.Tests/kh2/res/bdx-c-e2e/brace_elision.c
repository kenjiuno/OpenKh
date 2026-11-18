// bdEmu e2e: brace elision in initializers (multi-dimensional arrays, arrays of structs, nested structs, char and short members)
struct P { int x; int y; };
struct L { struct P a; struct P b; int t; };
struct N { int id; char name[4]; short v[2]; };
int g2[2][3] = { 1, 2, 3, 4, 5, 6 };
struct P gps[] = { 7, 8, 9, 10, 11, 12 };
void main() {
  int m[2][2] = { 1, 2, 3, 4 };
  int part[3][2] = { 1, 2, 3 };
  int mix[3][2] = { { 1 }, 5, 6, 7 };
  int inf[][3] = { 1, 2, 3, 4, 5 };
  struct P ps[2] = { 1, 2, 3, 4 };
  struct L l = { 1, 2, 3, 4, 5 };
  struct L l2 = { { 1, 2 }, 3, 4, 5 };
  struct L l3 = { 1, 2, { 3, 4 } };
  struct N n = { 9, "abc", 5, -6 };
  struct N ns[2] = { 1, "ab", 2, 3, 4, "cd", 5, 6 };
  char names[2][4] = { "ab", "cd" };
  short sh[2][2] = { 1, 2, 3, 4 };
  struct P cp = { 40, 41 };
  struct P cps[2] = { cp, 42, 43 };
  trap_puti(m[0][0] * 1000 + m[0][1] * 100 + m[1][0] * 10 + m[1][1], "m=");
  trap_puti(part[0][0] * 10000 + part[0][1] * 1000 + part[1][0] * 100 + part[1][1] * 10 + part[2][1], "part=");
  trap_puti(mix[0][0] * 10000 + mix[0][1] * 1000 + mix[1][0] * 100 + mix[1][1] * 10 + mix[2][0], "mix=");
  trap_puti(sizeof(inf) * 1000 + inf[1][0] * 100 + inf[1][1] * 10 + inf[1][2], "inf=");
  trap_puti(ps[0].x * 1000 + ps[0].y * 100 + ps[1].x * 10 + ps[1].y, "ps=");
  trap_puti(l.a.x * 10000 + l.a.y * 1000 + l.b.x * 100 + l.b.y * 10 + l.t, "l=");
  trap_puti(l2.a.x * 10000 + l2.a.y * 1000 + l2.b.x * 100 + l2.b.y * 10 + l2.t, "l2=");
  trap_puti(l3.a.x * 10000 + l3.a.y * 1000 + l3.b.x * 100 + l3.b.y * 10 + l3.t, "l3=");
  trap_puti(n.id * 1000 + n.name[1] + n.v[0] * 100 + n.v[1] + 1000, "n=");
  trap_puti(ns[0].id * 100000 + ns[0].v[0] * 10000 + ns[0].v[1] * 1000 + ns[1].id * 100 + ns[1].v[0] * 10 + ns[1].v[1], "ns=");
  trap_puti(ns[0].name[1] * 1000 + ns[1].name[0], "nm=");
  trap_puti(names[0][1] * 1000 + names[1][0], "names=");
  trap_puti(sh[0][0] * 1000 + sh[0][1] * 100 + sh[1][0] * 10 + sh[1][1], "sh=");
  trap_puti(cps[0].x * 1000 + cps[0].y * 100 + cps[1].x - 4200 + cps[1].y - 43, "cps=");
  trap_puti(g2[0][0] * 100000 + g2[0][2] * 10000 + g2[1][0] * 1000 + g2[1][2] * 100, "g2=");
  trap_puti(gps[0].x * 1000 + gps[1].y * 100 + gps[2].x - 11 + sizeof(gps), "gps=");
}
