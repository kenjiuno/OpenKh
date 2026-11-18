// bdEmu e2e: assignment value (a = b = c), compound assignment
void main() {
  int a = 1;
  int b;
  a += 2;
  b = a = a * 5;
  trap_puti(a, "a=");
  trap_puti(b, "b=");
}
