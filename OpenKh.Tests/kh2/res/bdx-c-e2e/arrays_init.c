// bdEmu e2e: array initializers (partial, [] length, nested, arrays of structs and pointers), array members of structs
struct P { int x; int y; };
struct B { int id; int v[3]; struct P pos; };
int gi[4] = { 1, 2, 3 };
int gn[] = { 5, 6, 7, 8, 9 };
struct P gps[2] = { { 1, 2 }, { 3, 4 } };
float gf[2] = { 1.5, 2.5 };
struct B gb = { 7, { 10, 20 }, { 5, 6 } };
int sum(int *p, int n) { int i; int s = 0; for (i = 0; i < n; i++) { s += p[i]; } return s; }
int bsum(struct B b) { return b.id * 1000 + b.v[0] + b.v[1] + b.v[2] + b.pos.x; }
void main() {
  int a[4] = { 9, 8 };
  int n[] = { 4, 5, 6 };
  int m[2][3] = { { 1, 2, 3 }, { 4, 5 } };
  struct P ps[3] = { { 1, 2 }, { 3 } };
  struct B lb = { 1, { 2, 3, 4 }, { 8, 9 } };
  struct B cp;
  int *ptrs[2] = { a, n };
  int i;
  trap_puti(sum(gi, 4), "gi=");
  trap_puti(sum(gn, 5), "gn=");
  trap_puti(gps[0].x * 1000 + gps[0].y * 100 + gps[1].x * 10 + gps[1].y, "gps=");
  trap_putf(gf[0] + gf[1], "gf=");
  trap_puti(gb.id * 1000 + gb.v[0] + gb.v[1] + gb.v[2] + gb.pos.y, "gb=");
  trap_puti(sum(a, 4), "a=");
  trap_puti(sum(n, 3), "n=");
  trap_puti(m[0][2] * 100 + m[1][0] * 10 + m[1][2], "m=");
  trap_puti(ps[0].y * 100 + ps[1].x * 10 + ps[2].x, "ps=");
  trap_puti(bsum(lb), "lb=");
  cp = gb;
  trap_puti(bsum(cp), "cp=");
  cp.v[1] = 99;
  trap_puti(bsum(cp) + bsum(gb), "cp2=");
  trap_puti(ptrs[0][1] * 10 + ptrs[1][2], "ptrs=");
  i = 1;
  ps[i].y = 55;
  gps[i].x += 40;
  trap_puti(ps[i].y + gps[i].x, "dyn=");
  lb.v[i + 1] = 77;
  trap_puti(lb.v[2] + lb.v[0], "lbv=");
}
