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
  trap_puti((a && b) * 100 + (a && 3) * 10 + (b && a), "and=");
  trap_puti((a || b) * 100 + (b || 0) * 10 + (b || a), "or=");
  trap_puti((7 || 0) + (0 || 9) * 10 + (9 && 7) * 100, "raw=");
  hits = 0;
  r = t(0) && t(1);
  trap_puti(r * 10 + hits, "sc1=");
  hits = 0;
  r = t(1) || t(1);
  trap_puti(r * 10 + hits, "sc2=");
  hits = 0;
  r = t(1) && t(2) && t(0) && t(5);
  trap_puti(r * 10 + hits, "sc3=");
  hits = 0;
  r = t(0) || t(0) || t(4);
  trap_puti(r * 10 + hits, "sc4=");
  trap_puti((a > 3 && b == 0) + (a < 3 || b != 0) * 10 + (a > 3 || b / 0) * 100, "mix=");
  trap_puti((p && p[0]) * 10 + (p || a), "ptr=");
  trap_puti((f && a) * 10 + (f || 0), "flt=");
  trap_puti(pick(1, 10, 20) * 100 + pick(0, 10, 20), "sel=");
  trap_puti(a > 3 ? (b ? 1 : 2) : 3, "nest=");
  hits = 0;
  r = a ? t(11) : t(22);
  trap_puti(r * 10 + hits, "selsc=");
  trap_puti((a > 3 ? a * 2 : a) + 1, "selexp=");
  if (a > 3 && b == 0) { trap_puti(1, "if="); }
  if (a < 3 || b != 0) { trap_puti(1, "if2="); } else { trap_puti(2, "if2="); }
  r = 0;
  while (r < 10 && hits < 100) { r++; }
  trap_puti(r, "loop=");
  trap_puti(len("abc") > 2 && len("abcd") == 4, "len=");
  trap_puti(1 ? 2 ? 3 : 4 : 5, "chain=");
  trap_puti((r = a > 3 ? 7 : 8), "asg=");
}
