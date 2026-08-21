# Spelling dictionary

Copied next to the executable into a `dictionary/` folder, where the word processor's spell
checker loads it from. Nothing here was written for this repository.

| File | What it is | Provenance |
| --- | --- | --- |
| `words.txt` | 370,105 lowercase English words, one per line | [dwyl/english-words](https://github.com/dwyl/english-words) (`words_alpha.txt`), released into the **public domain** under the Unlicense, which grants use, redistribution and modification "for any purpose, commercial or non-commercial". |

## Why this list and not a shorter one

A ten-thousand-word frequency list was the first choice, and it was rejected on licensing rather
than on merit. The obvious candidate (`first20hours/google-10000-english`) derives from the Google
Web Trillion Word Corpus and carries an explicit restriction: *"Educational and personal/research
use of this data is permitted... I do not recommend using this data for commercial purposes without
licensing it from the Linguistic Data Consortium."* This repository is MIT, so anything shipped
inside it is handed onward under terms that licence does not grant. It could not be used, and no
amount of it being only an example changes that.

The Unlicense is unambiguous by comparison, which is worth more here than a smaller file. **Check
the licence of a word list before the size of it**; they are data sets with real provenance, not
free-floating facts, and the permissive-looking ones frequently are not.

## What it costs, and what it buys

4.2 MB on disk, and roughly 25 MB of memory once it is a `HashSet`, which is why it is loaded lazily
on the first spell check rather than at start-up. For scale, the repository already tracks a single
5.6 MB GIF, so the file itself is unremarkable here.

Being near-exhaustive it almost never reports a real word as wrong, which is the failure that makes
a spell checker unusable. The trade is the other direction: a typo that happens to spell some
genuinely obscure word is accepted silently. That is the right way round.

It is a **word list, not a dictionary** in the sense of having definitions, parts of speech or
inflection rules. So the checker can say a word is not in it and can offer near-misses, and cannot
say what anything means. The thesaurus needs a different data set entirely.
