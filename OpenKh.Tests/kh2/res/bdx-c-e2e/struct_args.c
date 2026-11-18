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
  trap_puti(dot(a, b), "dot=");
  trap_puti(first(1, b, 2), "first=");
  clobber(a);
  trap_puti(a.x * 100 + a.y, "a=");
  trap_puti(sum(l), "sum=");
  trap_putf(getf(l), "f=");
  trap_puti(dot(g, l.a), "lit=");
  trap_puti(viaptr(&b), "viaptr=");
  trap_puti(fwd(a), "fwd=");
  trap_puti(dot(l.a, *l.next), "dot2=");
}
