// bdEmu e2e: preprocessor (object and function macros, multi-line #define, #undef, #if/#elif/#else, #ifdef, defined, __LINE__)
#define SIZE 4
#define MAX(a, b) ((a) > (b) ? (a) : (b))
#define SQUARE(x) ((x) * (x))
#define SUM3(a, b, c) (a + b + c)
#define LIMIT (SIZE * 2)
#define DEBUG 0
#define MODE 2
#define LONG_BODY \
  (SIZE + \
   LIMIT)
#undef DEBUG

int table[SIZE];

#if MODE == 1
int pick() { return 100; }
#elif MODE == 2 && defined(SIZE) && !defined(NOPE)
int pick() { return 200; }
#else
int pick() { return 300; }
#endif

#ifdef DEBUG
int debug() { return 1; }
#else
int debug() { return 0; }
#endif

void main() {
  int i;
  for (i = 0; i < SIZE; i++) { table[i] = SQUARE(i + 1); }
  trap_puti("table=", table[SIZE - 1] * 100 + table[1]);
  trap_puti("max=", MAX(table[1], table[2]) + MAX(3, MAX(9, 5)));
  trap_puti("sum=", SUM3(1, SQUARE(2), MAX(3, 4)) * 10 + LIMIT);
  trap_puti("long=", LONG_BODY);
  trap_puti("pick=", pick() + debug());
  trap_puti("line=", __LINE__);
}
