# Sample documents

Copied next to the executable into a `documents/` folder, which is where the word processor's
Open dialog starts. Nothing here was written for this repository; each file is fetched from a
source named below, and each is freely redistributable.

| File | What it is | Provenance |
| --- | --- | --- |
| `rfc1149.txt` | *A Standard for the Transmission of IP Datagrams on Avian Carriers*, the April Fools' RFC from 1990 | [rfc-editor.org](https://www.rfc-editor.org/rfc/rfc1149.txt). The memo itself states "Distribution of this memo is unlimited." |
| `rfc2549.txt` | The 1999 sequel, adding Quality of Service to the carrier pigeons | [rfc-editor.org](https://www.rfc-editor.org/rfc/rfc2549.txt) |
| `rfc6214.txt` | The 2011 IPv6 adaptation, because of course there is one | [rfc-editor.org](https://www.rfc-editor.org/rfc/rfc6214.txt) |
| `hamlet.txt` | *The Tragedy of Hamlet, Prince of Denmark* | The play body, cut out of the complete works at [MIT OCW](https://ocw.mit.edu/ans7870/6/6.006/s08/lecturenotes/files/t8.shakespeare.txt). Shakespeare died in 1616, so the text is public domain worldwide. |

## Why these four, and not one big file

They are two completely different shapes of document, which is the point.

The RFCs are **column-formatted**: hard-wrapped at 72 characters, with form feeds as page breaks
and a running header and footer on every page. That is a document that already knows how it wants
to be printed, and it is what the print feature is tested against.

`hamlet.txt` is **prose and dialogue**: speaker names, stage directions, indentation that carries
meaning, 4,550 lines and 32,630 words of it. It is the scrolling and load-time fixture, and it is
long enough that anything accidentally quadratic in the document model shows up immediately.

Every file is CRLF with no BOM and no tabs, so the editor's line-ending handling is exercised by
the samples rather than only by tests.
