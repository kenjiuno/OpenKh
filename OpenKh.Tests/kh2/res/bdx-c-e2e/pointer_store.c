// bdEmu e2e: stores through pointers (p->x = v, *q += v) and chained -> access
struct V { int x; int y; struct V *next; };
void setv(struct V *p, int x, int y) { p->x = x; p->y = y + 1; }
void inc(int *q) { *q += 5; }
int chain(struct V *p) { return (p->next->x = 99); }
void main() {
  struct V a; struct V b; int n = 10; int m = 20;
  a.next = &b; b.next = 0;
  setv(&a, 1, 2);
  setv(a.next, 30, 40);
  inc(&n);
  trap_puti(a.x, "a.x=");
  trap_puti(a.y, "a.y=");
  trap_puti(b.x, "b.x=");
  trap_puti(b.y, "b.y=");
  trap_puti(n, "n=");
  trap_puti(m, "m=");
  trap_puti(chain(&a), "chain=");
  trap_puti(b.x, "b.x2=");
}
