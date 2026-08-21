REM Asking questions. INPUT stops the program until you answer it.

CLS
COLOR 14, 1
PRINT " Twenty questions, minus eighteen "
COLOR 7, 0
PRINT

INPUT "What is your name"; NAME$
IF LEN(NAME$) = 0 THEN NAME$ = "nobody at all"
PRINT "Hello, "; NAME$; "."
PRINT

INPUT "Pick a whole number"; N
PRINT

IF N = 0 THEN
    PRINT "Zero it is. A fine number, if a quiet one."
ELSE
    PRINT "You picked"; N
    PRINT "Its square is"; N * N
    PRINT "Counting up to it, or as far as ten:"

    LIMIT = N
    IF LIMIT > 10 THEN LIMIT = 10
    IF LIMIT < 1 THEN LIMIT = 1

    FOR I = 1 TO LIMIT
        PRINT I;
    NEXT I
    PRINT
END IF

PRINT
PRINT "Thank you, "; NAME$; ". Press ESC to go back to the listing."
