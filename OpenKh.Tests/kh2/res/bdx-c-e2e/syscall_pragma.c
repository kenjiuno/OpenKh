// bdEmu e2e: #pragma defsyscall binds a name to a trap, and a prototype gives the types of its arguments
#pragma defsyscall show_int 0 0
void show_int(const char *name, int value);
#pragma defsyscall show_float 0 1
void show_float(const char *name, float value);
#pragma defsyscall show_text 0 2
void show_text(const char *text);

// a prototype of a trap of the table: the number stays, the types are these
void trap_puti(const char *name, int value);

void main() {
  int n = 7;
  show_int("i=", n);
  show_float("f=", 2);
  show_float("g=", n / 2);
  show_float("h=", 0.5 * 3);
  show_text("text");
  trap_puti("orig=", n + 1);
}
