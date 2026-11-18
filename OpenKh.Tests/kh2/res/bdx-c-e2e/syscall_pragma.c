// bdEmu e2e: #pragma defsyscall binds a name to a trap, and a prototype gives the types of its arguments
#pragma defsyscall show_int 0 0
void show_int(int value, const char *name);
#pragma defsyscall show_float 0 1
void show_float(float value, const char *name);
#pragma defsyscall show_text 0 2
void show_text(const char *text);

// a prototype of a trap of the table: the number stays, the types are these
void trap_puti(int value, const char *name);

void main() {
  int n = 7;
  show_int(n, "i=");
  show_float(2, "f=");
  show_float(n / 2, "g=");
  show_float(0.5 * 3, "h=");
  show_text("text");
  trap_puti(n + 1, "orig=");
}
