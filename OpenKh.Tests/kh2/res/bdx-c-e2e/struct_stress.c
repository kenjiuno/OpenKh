// bdEmu e2e: struct temporaries: calls in store targets, loops, conditions, recursion
struct P { int x; int y; };
struct V { int a; int b; int c; int d; };
struct P glob;
struct P other = { 5, 6 };
struct P *q() { struct P junk; junk.x = 1; return &glob; }
struct P mk(int x, int y) { struct P r; r.x = x; r.y = y; return r; }
struct V mkv(int k) { struct V v; v.a = k; v.b = k + 1; v.c = k + 2; v.d = k + 3; return v; }
int vsum(struct V v) { return v.a * 1000 + v.b * 100 + v.c * 10 + v.d; }
int add(int a, int b) { return a + b; }
int deep(int n) {
  struct P t = mk(n, n + 1);
  if (n == 0) { return t.x + t.y; }
  return deep(n - 1) + mk(n, 0).x;
}
void main() {
  int i; int total = 0;
  struct P a;
  *q() = mk(7, 8);                       // call in the store target while a temporary is alive
  trap_puti("glob=", glob.x * 100 + glob.y);
  a = mk(1, 2);
  q()->y = mk(3, 4).y + mk(5, 6).x;      // two temporaries, pointer store with a call
  trap_puti("q=", glob.x * 100 + glob.y);
  trap_puti("vsum=", vsum(mkv(1)));
  trap_puti("vsum2=", vsum(mkv(4)) + vsum(mkv(2)));
  for (i = 0; i < 3; i++) {
    struct P t = mk(i, i * 2);
    total += t.x + t.y + mk(i, 1).y;
  }
  trap_puti("loop=", total);
  switch (mk(2, 3).y) {
    case 3: total += 100; break;
    default: total = 0;
  }
  trap_puti("switch=", total);
  if (mk(1, 0).x) { struct P u = mk(9, 9); total += u.x; }
  while (mk(total, 0).x < 125) { struct P w = mk(1, 1); total += w.x; }
  trap_puti("cond=", total);
  trap_puti("deep=", deep(4));
  trap_puti("add=", add(mk(1, 2).y, add(mk(3, 4).x, mk(5, 6).y)));
  trap_puti("a=", a.x * 100 + a.y);
  other = *q();
  trap_puti("other=", other.x * 100 + other.y);
}
