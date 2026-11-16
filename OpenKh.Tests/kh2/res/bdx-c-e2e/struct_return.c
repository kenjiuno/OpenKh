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
  trap_puti("a=", a.x * 100 + a.y);
  trap_puti("b=", b.x * 100 + b.y);
  trap_puti("member=", make(5, 6).y);
  trap_puti("getg=", getg().x);
  c = getg();
  trap_puti("c=", c.x * 100 + c.y);
  trap_puti("deref=", deref(&a).y);
  trap_puti("pass=", pass(b).x);
  trap_puti("fwd=", fwd(7).x * 100 + fwd(7).y);
  l = mkl(9);
  trap_puti("l=", l.a.x * 100 + l.a.y + l.next->x * 10000);
  trap_putf("l.f=", l.f);
  trap_puti("arg=", sum(make(100, 23)));
  trap_puti("chain=", swapp(swapp(make(1, 2))).x);
  make(1, 2);
  g = make(11, 22);
  trap_puti("g=", g.x * 100 + g.y);
  trap_puti("both=", make(1, 2).x + make(30, 40).y);
  a = swapp(a);
  trap_puti("a2=", a.x * 100 + a.y);
  {
    struct L m = { make(1, 2), 2.5 };
    trap_puti("init=", m.a.x * 100 + m.a.y);
  }
}
