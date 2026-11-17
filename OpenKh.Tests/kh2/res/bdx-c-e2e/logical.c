// bdEmu e2e: &&, || (short circuit, 0 or 1) and ?: with ints, pointers, floats, calls, conditions and loops
int hits;
int t(int v) { hits = hits + 1; return v; }
int pick(int c, int a, int b) { return c ? a : b; }
int len(char *s) { int n = 0; while (s[n]) { n++; } return n; }
void main() {
  int a = 5;
  int b = 0;
  int *p = 0;
  float f = 2.5;
  int r;
  trap_puti("and=", (a && b) * 100 + (a && 3) * 10 + (b && a));
  trap_puti("or=", (a || b) * 100 + (b || 0) * 10 + (b || a));
  trap_puti("raw=", (7 || 0) + (0 || 9) * 10 + (9 && 7) * 100);
  hits = 0;
  r = t(0) && t(1);
  trap_puti("sc1=", r * 10 + hits);
  hits = 0;
  r = t(1) || t(1);
  trap_puti("sc2=", r * 10 + hits);
  hits = 0;
  r = t(1) && t(2) && t(0) && t(5);
  trap_puti("sc3=", r * 10 + hits);
  hits = 0;
  r = t(0) || t(0) || t(4);
  trap_puti("sc4=", r * 10 + hits);
  trap_puti("mix=", (a > 3 && b == 0) + (a < 3 || b != 0) * 10 + (a > 3 || b / 0) * 100);
  trap_puti("ptr=", (p && p[0]) * 10 + (p || a));
  trap_puti("flt=", (f && a) * 10 + (f || 0));
  trap_puti("sel=", pick(1, 10, 20) * 100 + pick(0, 10, 20));
  trap_puti("nest=", a > 3 ? (b ? 1 : 2) : 3);
  hits = 0;
  r = a ? t(11) : t(22);
  trap_puti("selsc=", r * 10 + hits);
  trap_puti("selexp=", (a > 3 ? a * 2 : a) + 1);
  if (a > 3 && b == 0) { trap_puti("if=", 1); }
  if (a < 3 || b != 0) { trap_puti("if2=", 1); } else { trap_puti("if2=", 2); }
  r = 0;
  while (r < 10 && hits < 100) { r++; }
  trap_puti("loop=", r);
  trap_puti("len=", len("abc") > 2 && len("abcd") == 4);
  trap_puti("chain=", 1 ? 2 ? 3 : 4 : 5);
  trap_puti("asg=", (r = a > 3 ? 7 : 8));
}
