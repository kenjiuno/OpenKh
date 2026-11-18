// bdEmu e2e: function pointers (typedefs, parameters, return values, arrays, struct members, globals, struct arguments and results through them)
typedef int (*BinOp)(int, int);
typedef void (*Hook)(int *);
struct Ops { int id; BinOp op; int (*neg)(int); };
int add(int a, int b) { return a + b; }
int sub(int a, int b) { return a - b; }
int mul(int a, int b) { return a * b; }
int negate(int a) { return 0 - a; }
int twice(int a) { return a * 2; }
void inc(int *p) { *p += 1; }
void dec(int *p) { *p -= 1; }
struct P { int x; int y; };
struct P mk(int x, int y) { struct P p; p.x = x; p.y = y; return p; }
struct P mk2(int x, int y) { struct P p; p.x = y; p.y = x; return p; }
int psum(struct P p) { return p.x * 10 + p.y; }
int apply(BinOp f, int a, int b) { return f(a, b); }
int apply2(int (*f)(int, int), int a, int b) { return (*f)(a, b) + 1; }
BinOp pick(int which) { if (which == 0) { return add; } if (which == 1) { return sub; } return 0; }
BinOp gtable[3] = { add, sub, mul };
BinOp gsingle = mul;
int count;
int (*gneg)(int) = negate;
void main() {
  BinOp op = add;
  BinOp ops[3];
  Hook h = inc;
  struct Ops s;
  struct P (*maker)(int, int) = mk;
  int (*pk)(struct P) = psum;
  int v = 5;
  int i;
  BinOp *pp;
  trap_puti("direct=", op(3, 4));
  op = sub;
  trap_puti("reassign=", op(10, 4));
  trap_puti("apply=", apply(add, 6, 7) * 100 + apply(mul, 6, 7));
  trap_puti("apply2=", apply2(sub, 9, 4));
  ops[0] = add; ops[1] = sub; ops[2] = mul;
  trap_puti("array=", ops[0](5, 6) * 10000 + ops[1](5, 6) * 1000 + ops[2](5, 6));
  for (i = 0; i < 3; i++) { count += gtable[i](8, 2); }
  trap_puti("gtable=", count);
  trap_puti("gsingle=", gsingle(6, 7) + pick(0)(1, 2) * 100 + pick(1)(9, 3) * 1000);
  h(&v);
  trap_puti("hook=", v);
  h = dec; h(&v); h(&v);
  trap_puti("hook2=", v);
  s.id = 3; s.op = mul; s.neg = twice;
  trap_puti("member=", s.op(s.id, 5) * 100 + s.neg(s.id));
  trap_puti("deref=", (*s.op)(4, 4) + (*gneg)(7) + 100);
  trap_puti("null=", (op == 0) * 10 + (pick(2) == 0));
  trap_puti("eq=", (op == sub) * 100 + (op == add) * 10 + (op != 0));
  trap_puti("addr=", (&add)(1, 1) + (&mul == mul));
  trap_puti("sret=", pk(maker(3, 4)) * 100 + psum(mk2(3, 4)));
  pp = &op;
  trap_puti("pp=", (*pp)(20, 5));
  if (op) { trap_puti("truthy=", 1); }
  trap_puti("sz=", sizeof(op) * 10 + sizeof(BinOp) + sizeof(s));
}
