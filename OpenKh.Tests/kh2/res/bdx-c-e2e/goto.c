// bdEmu e2e: goto and labels (forward, backward, out of nested loops, out of a switch)
int find(int *a, int n, int key) {
  int i, j;
  for (i = 0; i < n; i++) {
    for (j = 0; j < n; j++) {
      if (a[i] * 100 + a[j] == key) { goto found; }
    }
  }
  return -1;
found:
  return i * 10 + j;
}
int retry(int n) {
  int tries = 0;
again:
  tries++;
  if (tries < n) { goto again; }
  return tries;
}
int cleanup(int mode) {
  int r = 0;
  if (mode == 1) { goto fail; }
  r = r + 10;
  if (mode == 2) { goto fail; }
  r = r + 100;
  goto done;
fail:
  r = r + 5000;
done:
  return r;
}
void main() {
  int a[4];
  int i = 0;
  int sum = 0;
  a[0] = 1; a[1] = 2; a[2] = 3; a[3] = 4;
  trap_puti("find=", find(a, 4, 304));
  trap_puti("retry=", retry(7));
  trap_puti("clean=", cleanup(0) + cleanup(1) * 1000 + cleanup(2));
loop:
  if (i >= 5) { goto end; }
  sum += i;
  i++;
  goto loop;
end:
  trap_puti("loop=", sum);
  switch (sum) {
    case 10: goto in_switch;
    default: sum = 0;
  }
  trap_puti("skipped=", 1);
in_switch:
  trap_puti("sw=", sum);
}
