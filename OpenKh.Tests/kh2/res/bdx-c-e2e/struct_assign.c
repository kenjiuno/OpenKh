// bdEmu e2e: struct assignment and copies through pointers
struct P { int x; int y; };
struct L { struct P a; float f; struct P *next; };
struct P g1 = { 1, 2 };
struct P g2;
struct L gl = { { 5, 6 }, 1.5, &g1 };
void swap(struct P *a, struct P *b) { struct P t; t = *a; *a = *b; *b = t; }
void main() {
  struct P a = { 10, 20 };
  struct P b;
  struct P c = a;
  struct L l = { gl.a, 2.5 };
  struct L m;
  b = a;
  g2 = g1;
  a.x = 99;
  trap_puti("b=", b.x * 100 + b.y);
  trap_puti("c=", c.x * 100 + c.y);
  trap_puti("g2=", g2.x * 100 + g2.y);
  trap_puti("l.a=", l.a.x * 100 + l.a.y);
  m = gl;
  trap_puti("m=", m.a.x * 100 + m.a.y + m.next->y * 10000);
  trap_putf("m.f=", m.f);
  b = *gl.next;
  trap_puti("b2=", b.x * 100 + b.y);
  *gl.next = a;
  trap_puti("g1=", g1.x * 100 + g1.y);
  a.x = 1; a.y = 2; b.x = 3; b.y = 4;
  swap(&a, &b);
  trap_puti("swap=", a.x * 1000 + a.y * 100 + b.x * 10 + b.y);
  gl.a = g1;
  trap_puti("gl.a=", gl.a.x * 100 + gl.a.y);
}
