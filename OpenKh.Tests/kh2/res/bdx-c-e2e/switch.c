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
  trap_puti(classify(1), "c1=");
  trap_puti(classify(2), "c2=");
  trap_puti(classify(3), "c3=");
  trap_puti(classify(65), "cA=");
  trap_puti(classify(0 - 1), "c-1=");
  trap_puti(classify(9), "c9=");
  trap_puti(nodefault(5), "nd5=");
  trap_puti(nodefault(6), "nd6=");
  trap_puti(inloop(), "loop=");
  trap_puti(nested(1, 1), "n11=");
  trap_puti(nested(1, 2), "n12=");
  trap_puti(nested(1, 3), "n13=");
  trap_puti(nested(2, 4), "n24=");
  trap_puti(nested(3, 0), "n3=");
  trap_puti(dflt_first(1), "df1=");
  trap_puti(dflt_first(2), "df2=");
}
