// bdEmu e2e: short arrays (two elements per slot), short * indexing and stores, struct members, initializers
struct R { int id; short v[3]; char tag; };
short gh[5];
unsigned short gu[2] = { 40000, 7 };
int sum(short *p, int n) { int i; int s = 0; for (i = 0; i < n; i++) { s += p[i]; } return s; }
void main() {
  short a[5];
  short b[4] = { 1, -2, 300, -400 };
  unsigned short u[3];
  short c[] = { 10, 20, 30 };
  short m[2][3];
  struct R r;
  struct R q;
  short *p;
  int i;
  for (i = 0; i < 5; i++) { a[i] = i * 1000 - 2000; }
  trap_puti("a=", a[0] + 10000);
  trap_puti("a2=", a[2] + 10000);
  trap_puti("a4=", a[4]);
  trap_puti("neg=", a[1] < 0);
  trap_puti("sum=", sum(a, 5) + 10000);
  trap_puti("b=", b[0] * 1000000 + b[2] * 100 + b[1] + b[3] + 1000);
  u[0] = 65535; u[1] = 40000; u[2] = 1;
  trap_puti("u=", u[0] + u[1] + u[2]);
  trap_puti("gu=", gu[0] + gu[1]);
  trap_puti("c=", sum(c, 3) * 100 + sizeof(c));
  m[1][2] = 7; m[0][1] = -3; m[1][0] = 5;
  trap_puti("m=", m[1][2] * 100 + m[1][0] * 10 + m[0][1] + 10);
  gh[3] = 1234; gh[4] = -1;
  trap_puti("gh=", gh[3] * 10 + gh[4] + 10);
  p = a;
  p++;
  trap_puti("p=", *p + 10000);
  *p = 77; p[1] += 8; p[2]++;
  trap_puti("pp=", a[1] * 1000 + a[2] * 10 + a[3] + 20000);
  trap_puti("sz=", sizeof(a) * 100 + sizeof(m) + sizeof(struct R) * 1000);
  r.id = 9; r.tag = 'x';
  for (i = 0; i < 3; i++) { r.v[i] = i + 100; }
  q = r;
  r.v[0] = 0;
  trap_puti("st=", q.v[0] * 10000 + q.v[2] * 10 + q.id + q.tag - 'x');
  a[0] = 70000;
  trap_puti("wrap=", a[0] + 10000);
}
