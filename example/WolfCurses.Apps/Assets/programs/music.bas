REM PLAY takes a tune written as text: letters for notes, with octaves,
REM lengths, dots and a tempo. SOUND takes a pitch and a number of ticks.
REM
REM Nothing is audible yet. The only pitched sound reachable from a terminal
REM blocks for the length of each note, and this environment runs a program in
REM slices so that ESC keeps working, so a tune would freeze the very loop that
REM keeps the program interruptible. The notes are worked out and gone through
REM all the same, which is why a program with music in it runs correctly here
REM rather than not running.

CLS
COLOR 14, 1
PRINT " PLAY and SOUND "
COLOR 7, 0
PRINT

REM Tempo, default length and octave stay set until something changes them,
REM which is why they can be established on one line and used on every line
REM after it.
PLAY "T120 L4 O4"

PRINT "A scale, up and back down again:"
PRINT "  CDEFGAB>C<BAGFEDC"
PLAY "CDEFGAB>C<BAGFEDC"
PRINT

PRINT "The same notes as an arpeggio, twice as fast:"
PRINT "  L8 CEGE CEG>C"
PLAY "L8 CEGE CEG>C"
PLAY "L4"
PRINT

PRINT "Sharps and flats. These three are the same note:"
PRINT "  C#  C+  D-"
PLAY "C# C+ D-"
PRINT

PRINT "A dot makes a note half again as long, and P is a rest:"
PRINT "  C. P8 C2"
PLAY "C. P8 C2"
PRINT

REM Ode to Joy, from Beethoven's ninth symphony of 1824. Long out of
REM copyright, which is why it is safe to write down here.
PRINT "Beethoven, Ode to Joy (1824):"
PLAY "T100 L4 O4"
PLAY "EEFG GFED CCDE E.D8 D2"
PLAY "EEFG GFED CCDE D.C8 C2"
PRINT "  played, 2 phrases"
PRINT

REM The tune published in France in 1761 as "Ah! vous dirai-je, maman", which
REM the English-speaking world learned as Twinkle Twinkle. Also public domain.
PRINT "Ah! vous dirai-je, maman (1761):"
PLAY "T140 L4 O4"
PLAY "CCGG AAG2 FFEE DDC2"
PLAY "GGFF EED2 GGFF EED2"
PLAY "CCGG AAG2 FFEE DDC2"
PRINT "  played, 6 phrases"
PRINT

REM Grieg, In the Hall of the Mountain King, from the Peer Gynt music of 1875.
REM Grieg died in 1907, so this is public domain everywhere.
REM
REM The piece is one theme repeated faster and faster, which makes it the best
REM demonstration of T there is: the notes below never change, only the tempo.
PRINT "Grieg, In the Hall of the Mountain King (1875):"
PLAY "L8 O3"

REM Creeping about in the bass.
PLAY "T80"
PLAY "DEFGAFA B-GB-AFA"
PLAY "DEFGAFA B-GB-AFA"
PRINT "  T80,  in the cellar"

REM The same theme an octave up and half again as quick.
PLAY "T120 O4"
PLAY "DEFGAFA B-GB-AFA"
PLAY "DEFGAFA B-GB-AFA"
PRINT "  T120, one octave up"

REM And away it goes.
PLAY "T180 O5"
PLAY "DEFGAFA B-GB-AFA"
PLAY "T240 L16"
PLAY "DEFGAFA B-GB-AFA"
PLAY "DEFGAFA B-GB-AFA"
PRINT "  T180 then T240, running for the door"
PLAY "L4 O4 D2"
PRINT

REM A fanfare of no particular pedigree, written for this file.
PRINT "And a fanfare of our own:"
PLAY "T160 L8 O4 CEG>C<GEC L4 >C"
PRINT

REM SOUND asks for a pitch in hertz and a length in clock ticks, of which
REM there were 18.2 in a second. So 18 ticks is very nearly one second.
PRINT "SOUND takes hertz and ticks. 440 is the A above middle C:"
SOUND 440, 9
SOUND 494, 9
SOUND 523, 18
PRINT "  three notes, half a second each and then one whole one"
PRINT

REM Worked out rather than written down: a rising sweep, which is the sort of
REM thing SOUND is for and PLAY cannot say.
PRINT "A sweep, worked out a step at a time:"
FOR F = 200 TO 1200 STEP 50
    SOUND F, 1
NEXT F
PRINT "  21 steps from 200 Hz to 1200 Hz"
PRINT

PRINT "Press ESC to go back to the listing."
