// bdEmu e2e: arrays: indexing (constant and dynamic), decay to pointers, pointer arithmetic, 2D arrays, globals
int g[4];
int sum(int *p, int n) { int i; int s = 0; for (i = 0; i < n; i++) { s += p[i]; } return s; }
void main() {
  int a[5];
  int i;
  int m[2][3];
  for (i = 0; i < 5; i++) { a[i] = i * i; }
  for (i = 0; i < 4; i++) { g[i] = 10 + i; }
  a[2] += 100;
  trap_puti("a2=", a[2]);
  trap_puti("a4=", a[4]);
  trap_puti("sum=", sum(a, 5));
  trap_puti("gsum=", sum(g, 4));
  trap_puti("p=", *(a + 3));
  int *q = a + 1;
  trap_puti("q=", q[2]);
  trap_puti("deref=", *a + *g);
  m[1][2] = 7; m[0][1] = 3;
  trap_puti("m=", m[1][2] * 10 + m[0][1]);
  for (i = 0; i < 3; i++) { m[1][i] = i + 20; }
  trap_puti("row=", m[1][0] + m[1][1] + m[1][2]);
  trap_puti("dyn=", a[sum(g, 2) - 21]);
}
