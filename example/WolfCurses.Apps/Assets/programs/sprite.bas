REM GET lifts a rectangle of the screen into an array and PUT stamps it back.
REM It is how every BASIC game ever written moved something about.

SCREEN 13

REM Draw the thing once, off in a corner, then take a copy of it.
CIRCLE (10, 10), 8, 14
PAINT (10, 10), 14, 14
CIRCLE (7, 8), 2, 0
CIRCLE (13, 8), 2, 0

DIM FACE(600)
GET (0, 0)-(20, 20), FACE

REM Rub out the original so what moves below came out of the array.
LINE (0, 0)-(20, 20), 0, BF

REM A backdrop, so there is something for the sprite to pass in front of.
FOR I = 0 TO 15
    LINE (0, 60 + I * 8)-(319, 60 + I * 8), I
NEXT I

REM Now walk it across. PUT defaults to XOR, so stamping the same sprite in
REM the same place a second time puts the screen back exactly as it was, and
REM nothing has to remember what was underneath.
FOR X = 0 TO 280 STEP 8
    PUT (X, 90), FACE
    PUT (X, 90), FACE
NEXT X

REM Leave it sitting at the end of its walk.
PUT (288, 90), FACE

REM A short tune. Nothing is audible yet, but the notes are worked out.
PLAY "T140 L8 O4 CDEFGAB O5 C"

LOCATE 1, 1
PRINT "GET and PUT"
