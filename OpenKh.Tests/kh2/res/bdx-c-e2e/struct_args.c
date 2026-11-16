// bdEmu e2e: structs passed by value
struct P { int x; int y; };
struct L { struct P a; float f; struct P *next; };
struct P g = { 7, 8 };
int dot(struct P a, struct P b) { return a.x * b.x + a.y * b.y; }
int first(int k, struct P p, int z) { return k * 10000 + p.x * 100 + p.y + z * 1000000; }
void clobber(struct P p) { p.x = 999; p.y = 888; }
int sum(struct L l) { return l.a.x + l.a.y + l.next->y; }
float getf(struct L l) { return l.f; }
int viaptr(struct P *pp) { return dot(*pp, *pp); }
int fwd(struct P p) { return dot(p, p); }
void main() {
  struct P a = { 3, 4 };
  struct P b = { 5, 6 };
  struct L l = { { 1, 2 }, 2.5, &g };
  trap_puti("dot=", dot(a, b));
  trap_puti("first=", first(1, b, 2));
  clobber(a);
  trap_puti("a=", a.x * 100 + a.y);
  trap_puti("sum=", sum(l));
  trap_putf("f=", getf(l));
  trap_puti("lit=", dot(g, l.a));
  trap_puti("viaptr=", viaptr(&b));
  trap_puti("fwd=", fwd(a));
  trap_puti("dot2=", dot(l.a, *l.next));
}
