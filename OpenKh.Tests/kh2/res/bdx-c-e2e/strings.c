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
  trap_puti(len(gs), "len=");
  trap_puti(word[0], "w0=");
  trap_puti(word[2], "w2=");
  trap_puti(word[3], "w3=");
  trap_puti(word[7], "w7=");
  trap_puti(len(t), "tl=");
  t[1] = 'Y';
  trap_puti(t[0] * 1000000 + t[1] * 1000 + t[2], "t=");
  trap_puti(b[1], "b1=");
  copy(buf, "hello world");
  trap_puti(len(buf), "blen=");
  trap_puti(buf[10], "b10=");
  p = buf;
  p++;
  trap_puti(*p, "p=");
  *p = 'E';
  trap_puti(buf[1], "e=");
  buf[0] = 200;
  u[0] = 200;
  trap_puti(buf[0] + 256, "sg=");
  trap_puti(buf[0] < 0, "neg=");
  trap_puti(u[0], "un=");
  copy(g, gs);
  g[0] = 'J';
  trap_puti(g[0] * 1000 + g[4], "g=");
  trap_puts(buf + 1);
  trap_puts("lit");
}
