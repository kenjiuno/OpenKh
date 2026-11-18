// bdEmu e2e: structs returned by value: locals, params, globals, *p, pass-through, members, arguments
struct P { int x; int y; };
struct L { struct P a; float f; struct P *next; };
struct P g = { 70, 80 };
struct P make(int x, int y) { struct P r; r.x = x; r.y = y; return r; }
struct P getg() { return g; }
struct P deref(struct P *p) { return *p; }
struct P pass(struct P p) { return p; }
struct P fwd(int a) { return make(a, a + 1); }
struct L mkl(int k) { struct L l = { { k, k * 2 }, 1.5, &g }; return l; }
struct P swapp(struct P p) { struct P r; r.x = p.y; r.y = p.x; return r; }
int sum(struct P p) { return p.x + p.y; }
void main() {
  struct P a = make(3, 4);
  struct P b;
  struct P c;
  struct L l;
  b = make(10, 20);
  trap_puti(a.x * 100 + a.y, "a=");
  trap_puti(b.x * 100 + b.y, "b=");
  trap_puti(make(5, 6).y, "member=");
  trap_puti(getg().x, "getg=");
  c = getg();
  trap_puti(c.x * 100 + c.y, "c=");
  trap_puti(deref(&a).y, "deref=");
  trap_puti(pass(b).x, "pass=");
  trap_puti(fwd(7).x * 100 + fwd(7).y, "fwd=");
  l = mkl(9);
  trap_puti(l.a.x * 100 + l.a.y + l.next->x * 10000, "l=");
  trap_putf(l.f, "l.f=");
  trap_puti(sum(make(100, 23)), "arg=");
  trap_puti(swapp(swapp(make(1, 2))).x, "chain=");
  make(1, 2);
  g = make(11, 22);
  trap_puti(g.x * 100 + g.y, "g=");
  trap_puti(make(1, 2).x + make(30, 40).y, "both=");
  a = swapp(a);
  trap_puti(a.x * 100 + a.y, "a2=");
  {
    struct L m = { make(1, 2), 2.5 };
    trap_puti(m.a.x * 100 + m.a.y, "init=");
  }
}
