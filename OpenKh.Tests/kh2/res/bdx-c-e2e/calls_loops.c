// bdEmu e2e: jal calls, recursion, for/while/break/continue, string literal
int sub(int a, int b) { return a - b; }
int fact(int n) { if (n <= 1) { return 1; } return n * fact(n - 1); }
void main() {
  int i; int s = 0;
  trap_puti("sub=", sub(10, 3));
  trap_puti("fact=", fact(5));
  for (i = 0; i < 5; i++) { s += i; }
  trap_puti("sum=", s);
  i = 0;
  while (1) { i++; if (i > 7) break; if (i == 3) continue; s += 100; }
  trap_puti("loop=", s);
  trap_puts("hello");
}
