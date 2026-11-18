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
  trap_puti(a[2], "a2=");
  trap_puti(a[4], "a4=");
  trap_puti(sum(a, 5), "sum=");
  trap_puti(sum(g, 4), "gsum=");
  trap_puti(*(a + 3), "p=");
  int *q = a + 1;
  trap_puti(q[2], "q=");
  trap_puti(*a + *g, "deref=");
  m[1][2] = 7; m[0][1] = 3;
  trap_puti(m[1][2] * 10 + m[0][1], "m=");
  for (i = 0; i < 3; i++) { m[1][i] = i + 20; }
  trap_puti(m[1][0] + m[1][1] + m[1][2], "row=");
  trap_puti(a[sum(g, 2) - 21], "dyn=");
}
