REM Drawing. SCREEN picks a graphics mode and everything below draws into it.
REM What comes out is real pixels where the terminal can manage them, and half
REM blocks where it cannot, which WolfCurses decides for itself.

SCREEN 13

REM A frame around the whole screen.
LINE (0, 0)-(319, 199), 15, B

REM Boxes, one filled and one not, to show what B and BF each do.
LINE (20, 20)-(90, 70), 9, BF
LINE (20, 90)-(90, 140), 9, B

REM A fan of lines from one point, each a slightly different colour.
FOR I = 0 TO 15
    LINE (160, 100)-(160 + I * 10, 20), I
NEXT I

REM Circles inside one another. CIRCLE draws an outline, so they stay hollow.
FOR R = 10 TO 50 STEP 10
    CIRCLE (250, 150), R, 14
NEXT R

REM PAINT floods an area and stops when it meets the border colour.
CIRCLE (60, 165), 25, 12
PAINT (60, 165), 4, 12

REM A curve worked out a point at a time, which is all PSET needs.
FOR X = 0 TO 319
    Y = 100 + SIN(X / 12) * 30
    PSET (X, Y), 10
NEXT X

REM Press ESC to come back to the listing.
