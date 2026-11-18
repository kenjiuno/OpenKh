// bdEmu e2e: braced struct initializers: nested, partial, zero fill, globals and locals
struct P { int x; int y; };
struct L { struct P a; float f; struct P *next; int tail; };
struct P gp = { 3, 4 };
struct L gl = { { 1, 2 }, 2.5, &gp };
struct P gz = { 9 };
int gi = { 7 };
void main() {
  struct P lp = { 10, 20 };
  struct L ll = { { 5 }, 1.5 };
  struct L zz = { };
  trap_puti(gp.x + gp.y * 10, "gp=");
  trap_puti(gl.a.x + gl.a.y * 10, "gl.a=");
  trap_putf(gl.f, "gl.f=");
  trap_puti(gl.next->y, "gl.next=");
  trap_puti(gl.tail, "gl.tail=");
  trap_puti(gz.x * 10 + gz.y, "gz=");
  trap_puti(gi, "gi=");
  trap_puti(lp.x + lp.y * 100, "lp=");
  trap_puti(ll.a.x * 10 + ll.a.y, "ll.a=");
  trap_putf(ll.f, "ll.f=");
  trap_puti(ll.next == 0, "ll.next=");
  trap_puti(zz.a.x + zz.a.y + zz.tail, "zz=");
}
