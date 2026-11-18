// bdEmu e2e: enum (values, explicit values, anonymous enum, switch cases, array lengths, struct members, shadowing)
enum Color { RED, GREEN, BLUE };
enum Level { LOW = 10, MID = 20, HIGH = LOW + MID, NEG = -5, AFTER };
enum { ANON_A = 3, ANON_B };
enum Color gc = BLUE;
int table[BLUE + 2];
struct Item { enum Color color; int level; char tag[ANON_A + 1]; };
enum Color next(enum Color c) {
  switch (c) {
    case RED: return GREEN;
    case GREEN: return BLUE;
    default: return RED;
  }
}
int weight(enum Level l) { return l == HIGH ? 3 : l == MID ? 2 : 1; }
void main() {
  enum Color c = RED;
  enum Level l = MID;
  struct Item it;
  int i;
  int mix = GREEN + BLUE * 10 + HIGH * 100;
  trap_puti("vals=", RED * 100000 + GREEN * 10000 + BLUE * 1000 + LOW + 0);
  trap_puti("level=", HIGH + MID + LOW + NEG + AFTER);
  trap_puti("anon=", ANON_A * 10 + ANON_B);
  trap_puti("gc=", gc);
  trap_puti("sz=", sizeof(table) * 10 + sizeof(enum Color) + sizeof(it));
  c = next(c);
  trap_puti("next=", c * 10 + next(c));
  trap_puti("next3=", next(next(next(c))));
  trap_puti("w=", weight(HIGH) * 100 + weight(l) * 10 + weight(LOW));
  it.color = BLUE; it.level = HIGH; it.tag[ANON_A] = 'q';
  trap_puti("it=", it.color * 10000 + it.level * 100 + it.tag[ANON_A] - 100);
  for (i = RED; i <= BLUE; i++) { table[i] = i * 2; }
  trap_puti("tab=", table[GREEN] * 100 + table[BLUE]);
  trap_puti("mix=", mix);
  trap_puti("cmp=", (c == GREEN) * 10 + (c != BLUE));
  {
    int RED = 77;
    trap_puti("shadow=", RED);
  }
  trap_puti("after=", RED + 5);
}
