// bdEmu e2e: global initializers run by OBJ_INIT (--trigger0)
int g = 5;
int h;
void main() { h = g + 1; trap_puti("g=", g); trap_puti("h=", h); }
void other() { trap_puts("other"); }
