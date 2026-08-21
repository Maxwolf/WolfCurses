REM Drawing with nothing but PRINT and a loop.

CLS
COLOR 11, 0
PRINT "A triangle:"
COLOR 7, 0

FOR ROW = 1 TO 8
    PRINT SPACE$(8 - ROW); STRING$(ROW * 2 - 1, "*")
NEXT ROW

PRINT
COLOR 11, 0
PRINT "A square, drawn by putting the cursor where each edge goes:"
COLOR 7, 0

TOP = 14
LEFTEDGE = 5
SIDE = 9

FOR I = 0 TO SIDE - 1
    LOCATE TOP, LEFTEDGE + I
    PRINT "-";
    LOCATE TOP + 5, LEFTEDGE + I
    PRINT "-";
NEXT I

FOR I = 0 TO 5
    LOCATE TOP + I, LEFTEDGE
    PRINT "|";
    LOCATE TOP + I, LEFTEDGE + SIDE - 1
    PRINT "|";
NEXT I

LOCATE TOP + 7, 1
PRINT "LOCATE is what lets a program go back and change part of its screen."
