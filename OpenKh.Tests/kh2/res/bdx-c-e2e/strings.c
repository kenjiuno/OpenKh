// bdEmu e2e: char arrays (packed bytes), string initializers, byte loads and stores through char *, string literals
char g[16];
char gs[] = "Hello";
int len(char *s) { int n = 0; while (s[n]) { n++; } return n; }
void copy(char *d, char *s) { int i = 0; while (s[i]) { d[i] = s[i]; i++; } d[i] = 0; }
void main() {
  char buf[16];
  char word[8] = "abc";
  char t[] = "xyz";
  char b[4] = { 'a', 'b', 0, 0 };
  unsigned char u[2];
  char *p;
  trap_puti("len=", len(gs));
  trap_puti("w0=", word[0]);
  trap_puti("w2=", word[2]);
  trap_puti("w3=", word[3]);
  trap_puti("w7=", word[7]);
  trap_puti("tl=", len(t));
  t[1] = 'Y';
  trap_puti("t=", t[0] * 1000000 + t[1] * 1000 + t[2]);
  trap_puti("b1=", b[1]);
  copy(buf, "hello world");
  trap_puti("blen=", len(buf));
  trap_puti("b10=", buf[10]);
  p = buf;
  p++;
  trap_puti("p=", *p);
  *p = 'E';
  trap_puti("e=", buf[1]);
  buf[0] = 200;
  u[0] = 200;
  trap_puti("sg=", buf[0] + 256);
  trap_puti("neg=", buf[0] < 0);
  trap_puti("un=", u[0]);
  copy(g, gs);
  g[0] = 'J';
  trap_puti("g=", g[0] * 1000 + g[4]);
  trap_puts(buf + 1);
  trap_puts("lit");
}
