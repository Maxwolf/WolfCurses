REM SUBs and FUNCTIONs. A SUB does something, a FUNCTION works something out.

CLS
COLOR 14, 1
PRINT " Procedures "
COLOR 7, 0
PRINT

REM A SUB is called by name, with or without the word CALL.
Banner "Squares and triangles"
CALL Banner("Called the other way round")
PRINT

REM A FUNCTION hands a value back by assigning to its own name.
FOR I = 1 TO 6
    PRINT I, Square(I), Triangle(I)
NEXT I
PRINT

REM Variables inside a procedure are its own. This I is untouched by the one
REM the counting SUB uses, which is the whole reason procedures are worth having.
I = 999
Count 4
PRINT "Out here I is still"; I
PRINT

REM SHARED is how a procedure reaches a variable outside itself.
TOTAL = 0
FOR I = 1 TO 5
    AddToTotal I
NEXT I
PRINT "The SUB added up to"; TOTAL
PRINT

REM A FUNCTION may call itself, because every call gets its own locals.
PRINT "Six factorial is"; Factorial(6)

END

SUB Banner (TEXT$)
    PRINT STRING$(LEN(TEXT$) + 4, "-")
    PRINT "| "; TEXT$; " |"
    PRINT STRING$(LEN(TEXT$) + 4, "-")
END SUB

SUB Count (HOWMANY)
    FOR I = 1 TO HOWMANY
        PRINT "counting"; I
    NEXT I
END SUB

SUB AddToTotal (N)
    SHARED TOTAL
    TOTAL = TOTAL + N
END SUB

FUNCTION Square (N)
    Square = N * N
END FUNCTION

FUNCTION Triangle (N)
    Triangle = N * (N + 1) / 2
END FUNCTION

FUNCTION Factorial (N)
    IF N <= 1 THEN
        Factorial = 1
    ELSE
        Factorial = N * Factorial(N - 1)
    END IF
END FUNCTION
