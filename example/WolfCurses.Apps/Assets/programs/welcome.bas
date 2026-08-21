REM Welcome to BASIC, running inside WolfCurses.
REM Press F5 to run this, ESC to come back to the listing.

CLS
COLOR 14, 1
PRINT "  WolfCurses BASIC  "
COLOR 7, 0
PRINT

PRINT "Counting, because every BASIC starts here:"
FOR I = 1 TO 10
    PRINT I;
NEXT I
PRINT
PRINT

PRINT "The seven times table, laid out with commas:"
FOR I = 1 TO 5
    PRINT I, I * 7
NEXT I
PRINT

REM Strings, and the functions that pick them apart.
W$ = "WOLFCURSES"
PRINT "The word is "; W$
PRINT "Its first four letters are "; LEFT$(W$, 4)
PRINT "Letter five onward is "; MID$(W$, 5)
PRINT "It is"; LEN(W$); "characters long."
PRINT

REM A loop with a test, and a decision inside it.
PRINT "Odd and even, up to eight:"
N = 1
DO WHILE N <= 8
    IF N MOD 2 = 0 THEN
        PRINT N; "is even"
    ELSE
        PRINT N; "is odd"
    END IF
    N = N + 1
LOOP
PRINT

PRINT "That is the whole language so far. Try editing this and pressing F5 again."
