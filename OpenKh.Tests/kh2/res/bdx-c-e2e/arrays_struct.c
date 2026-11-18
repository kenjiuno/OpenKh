// bdEmu e2e: arrays of structs: pointer arithmetic by struct size, p[i], copies of elements
struct P { int x; int y; };
struct P gps[4] = { { 1, 2 }, { 3, 4 }, { 5, 6 }, { 7, 8 } };
int total(struct P *p, int n) { int s = 0; int i; for (i = 0; i < n; i++) { s += p->x * 10 + p->y; p = p + 1; } return s; }
struct P pick(struct P *p, int i) { return p[i]; }
void main() {
  struct P ps[3] = { { 10, 20 }, { 30, 40 }, { 50, 60 } };
  struct P *q = ps;
  int i;
  trap_puti(total(ps, 3), "total=");
  trap_puti(total(gps, 4), "gtotal=");
  trap_puti((q + 1)->y + q[2].x, "p1=");
  trap_puti((*(q + 2)).y, "deref=");
  trap_puti(pick(gps, 3).x * 10 + pick(ps, 1).y, "pick=");
  q = &ps[1];
  q->x = 99;
  trap_puti(ps[1].x + ps[0].x, "set=");
  ps[2] = gps[0];
  trap_puti(ps[2].x * 10 + ps[2].y, "copy=");
  for (i = 0; i < 3; i++) { gps[i] = ps[i]; }
  trap_puti(gps[1].x + gps[3].x, "gps=");
}
