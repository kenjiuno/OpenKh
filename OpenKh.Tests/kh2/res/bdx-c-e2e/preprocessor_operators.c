// bdEmu e2e: preprocessor operators: # (string), ## (paste) and variable arguments (...)
#define CAT(a, b) a ## b
#define XCAT(a, b) CAT(a, b)
#define STR(x) #x
#define XSTR(x) STR(x)
#define COUNT 3
#define FIELD(name, n) int name ## _ ## n
#define CALL(f, ...) f(__VA_ARGS__)
#define SHOW(fmt, ...) trap_puti(fmt, ## __VA_ARGS__)
#define ALL(...) sum4(__VA_ARGS__)

struct S { FIELD(a, 1); FIELD(b, 2); };
int CAT(value, 1) = 10;
int XCAT(value, COUNT) = 30;

int add(int a, int b) { return a + b; }
int sum4(int a, int b, int c, int d) { return a * 1000 + b * 100 + c * 10 + d; }

void main() {
  struct S s;
  s.a_1 = 4; s.b_2 = 5;
  trap_puts(STR(hello   world));
  trap_puts(XSTR(COUNT));
  trap_puts(XSTR(CAT(va, lue)));
  trap_puti("v=", value1 + value3);
  trap_puti("fields=", s.a_1 * 10 + s.b_2);
  trap_puti("call=", CALL(add, 2, 3) * 100 + CALL(sum4, 1, 2, 3, 4));
  SHOW("x=", 7);
  trap_puti("all=", ALL(5, 6, 7, 8));
  trap_puti("paste=", CAT(1, 2) + CAT(0x, 10));
}
