// bdEmu e2e: usual arithmetic conversions between int and float
void main() {
  int n = 3;
  float f = 0.5;
  float a = 3 * 0.5;
  float b = 0.5 * 3;
  float c = n * f;
  float d = f * n;
  float e = n + f;
  float g = f - n;
  float h = n / 2.0;
  float i = 7 / 2.0;
  int r1 = (f < n);
  int r2 = (n < f);
  int r3 = (f == 0.5);
  int r4 = (n != f);
  int r5 = (n == 3.0);
  trap_putf(a, "a="); trap_putf(b, "b="); trap_putf(c, "c="); trap_putf(d, "d=");
  trap_putf(e, "e="); trap_putf(g, "g="); trap_putf(h, "h="); trap_putf(i, "i=");
  trap_puti(r1 * 10000 + r2 * 1000 + r3 * 100 + r4 * 10 + r5, "cmp=");
  int a2 = 10; a2 += 0.5; trap_puti(a2, "a+=");
  int b2 = 10; b2 *= 1.5; trap_puti(b2, "b*=");
  int c2 = 10; c2 -= 2.5; trap_puti(c2, "c-=");
  int d2 = 10; d2 /= 4.0; trap_puti(d2, "d/=");
  float f2 = 1; f2 += 2; trap_putf(f2, "f+=");
  int e2 = 7; e2 += 3; trap_puti(e2, "e+=");
}
