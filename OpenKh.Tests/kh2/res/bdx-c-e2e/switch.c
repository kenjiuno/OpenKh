// bdEmu e2e: switch: fall-through, default, negative and char cases, nesting, continue in a loop
int classify(int n) {
  int r = 0;
  switch (n) {
    case 1: r = 10; break;
    case 2: r = 20;           // falls through
    case 3: r += 5; break;
    case 'A': r = 65; break;
    case -1: r = 99; break;
    default: r = 7;
  }
  return r;
}
int nodefault(int n) {
  int r = 1;
  switch (n) { case 5: r = 2; break; }
  return r;
}
int inloop() {
  int i; int s = 0;
  for (i = 0; i < 6; i++) {
    switch (i) {
      case 1: continue;            // continue belongs to the loop
      case 4: break;               // break leaves the switch only
      default: s += 10;
    }
    s += 1;
  }
  return s;
}
int nested(int a, int b) {
  switch (a) {
    case 1:
      switch (b) { case 1: return 11; case 2: return 12; }
      return 10;
    case 2: { int t = b * 2; return t; }
  }
  return -1;
}
int dflt_first(int n) {
  switch (n) { default: return 100; case 1: return 1; }
}
void main() {
  trap_puti("c1=", classify(1));
  trap_puti("c2=", classify(2));
  trap_puti("c3=", classify(3));
  trap_puti("cA=", classify(65));
  trap_puti("c-1=", classify(0 - 1));
  trap_puti("c9=", classify(9));
  trap_puti("nd5=", nodefault(5));
  trap_puti("nd6=", nodefault(6));
  trap_puti("loop=", inloop());
  trap_puti("n11=", nested(1, 1));
  trap_puti("n12=", nested(1, 2));
  trap_puti("n13=", nested(1, 3));
  trap_puti("n24=", nested(2, 4));
  trap_puti("n3=", nested(3, 0));
  trap_puti("df1=", dflt_first(1));
  trap_puti("df2=", dflt_first(2));
}
