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
  trap_puti(RED * 100000 + GREEN * 10000 + BLUE * 1000 + LOW + 0, "vals=");
  trap_puti(HIGH + MID + LOW + NEG + AFTER, "level=");
  trap_puti(ANON_A * 10 + ANON_B, "anon=");
  trap_puti(gc, "gc=");
  trap_puti(sizeof(table) * 10 + sizeof(enum Color) + sizeof(it), "sz=");
  c = next(c);
  trap_puti(c * 10 + next(c), "next=");
  trap_puti(next(next(next(c))), "next3=");
  trap_puti(weight(HIGH) * 100 + weight(l) * 10 + weight(LOW), "w=");
  it.color = BLUE; it.level = HIGH; it.tag[ANON_A] = 'q';
  trap_puti(it.color * 10000 + it.level * 100 + it.tag[ANON_A] - 100, "it=");
  for (i = RED; i <= BLUE; i++) { table[i] = i * 2; }
  trap_puti(table[GREEN] * 100 + table[BLUE], "tab=");
  trap_puti(mix, "mix=");
  trap_puti((c == GREEN) * 10 + (c != BLUE), "cmp=");
  {
    int RED = 77;
    trap_puti(RED, "shadow=");
  }
  trap_puti(RED + 5, "after=");
}
