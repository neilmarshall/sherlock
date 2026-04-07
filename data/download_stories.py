#!/usr/bin/env python3
"""
download_stories.py
───────────────────
Downloads all 56 Sherlock Holmes short stories from Project Gutenberg
(public domain — Arthur Conan Doyle died 1930) and saves each as a
separate plain-text file inside a 'stories/' sub-folder.

Usage (run in Terminal from the folder containing this script):
    python3 download_stories.py

Requirements: Python 3.6+ with no extra packages (uses stdlib only).
"""

import urllib.request
import re
import os
import time

# Stories will be saved here (relative to this script's location)
OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "stories")
os.makedirs(OUTPUT_DIR, exist_ok=True)


# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────

def fetch_text(url: str) -> str:
    print(f"  ↓ Fetching: {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (compatible)"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        raw = resp.read()
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        text = raw.decode("latin-1")
    # Normalise line endings so regex anchors work consistently
    return text.replace("\r\n", "\n").replace("\r", "\n")


def strip_gutenberg_boilerplate(text: str) -> str:
    """Remove Project Gutenberg header and footer."""
    start = re.search(
        r"\*{3}\s*START OF (THE|THIS) PROJECT GUTENBERG EBOOK.*?\*{3}",
        text, re.IGNORECASE
    )
    if start:
        text = text[start.end():]

    end = re.search(
        r"\*{3}\s*END OF (THE|THIS) PROJECT GUTENBERG EBOOK.*?\*{3}",
        text, re.IGNORECASE
    )
    if end:
        text = text[:end.start()]

    return text.strip()


def safe_filename(title: str) -> str:
    safe = re.sub(r'[\\/*?:"<>|]', "", title).strip()
    safe = re.sub(r"\s+", "_", safe)
    return safe[:100] + ".txt"


def save_story(title: str, collection: str, body: str) -> str:
    header = (
        f"Title: {title}\n"
        f"Collection: {collection}\n"
        f"Author: Arthur Conan Doyle\n"
        f"Source: Project Gutenberg (Public Domain)\n"
        f"\n{'=' * 60}\n\n"
    )
    path = os.path.join(OUTPUT_DIR, safe_filename(title))
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(header + body.strip() + "\n")
    return path


# ─────────────────────────────────────────────────────────────────────────────
# Story catalogue — 5 collections, 56 stories total
# ─────────────────────────────────────────────────────────────────────────────

COLLECTIONS = [
    {
        "name": "The Adventures of Sherlock Holmes",
        "url": "https://www.gutenberg.org/files/1661/1661-0.txt",
        "stories": [
            "A Scandal in Bohemia",
            "The Red-Headed League",
            "A Case of Identity",
            "The Boscombe Valley Mystery",
            "The Five Orange Pips",
            "The Man with the Twisted Lip",
            "The Adventure of the Blue Carbuncle",
            "The Adventure of the Speckled Band",
            "The Adventure of the Engineer's Thumb",
            "The Adventure of the Noble Bachelor",
            "The Adventure of the Beryl Coronet",
            "The Adventure of the Copper Beeches",
        ],
    },
    {
        "name": "The Memoirs of Sherlock Holmes",
        "url": "https://www.gutenberg.org/files/834/834-0.txt",
        "stories": [
            "Silver Blaze",
            "The Adventure of the Cardboard Box",  # Gutenberg #834 includes this here
            "The Yellow Face",
            "The Stock-Broker's Clerk",
            'The "Gloria Scott"',
            "The Musgrave Ritual",
            "The Reigate Squires",   # Gutenberg #834 uses this title (US: "The Reigate Puzzle")
            "The Crooked Man",
            "The Resident Patient",
            "The Greek Interpreter",
            "The Naval Treaty",
            "The Final Problem",
        ],
    },
    {
        "name": "The Return of Sherlock Holmes",
        "url": "https://www.gutenberg.org/files/108/108-0.txt",
        "stories": [
            "The Adventure of the Empty House",
            "The Adventure of the Norwood Builder",
            "The Adventure of the Dancing Men",
            "The Adventure of the Solitary Cyclist",
            "The Adventure of the Priory School",
            "The Adventure of Black Peter",
            "The Adventure of Charles Augustus Milverton",
            "The Adventure of the Six Napoleons",
            "The Adventure of the Three Students",
            "The Adventure of the Golden Pince-Nez",
            "The Adventure of the Missing Three-Quarter",
            "The Adventure of the Abbey Grange",
            "The Adventure of the Second Stain",
        ],
    },
    {
        "name": "His Last Bow",
        "url": "https://www.gutenberg.org/files/2350/2350-0.txt",
        "stories": [
            "The Adventure of Wisteria Lodge",
            "The Adventure of the Red Circle",
            "The Adventure of the Bruce-Partington Plans",
            "The Adventure of the Dying Detective",
            "The Disappearance of Lady Frances Carfax",
            "The Adventure of the Devil's Foot",
            "His Last Bow",
        ],
    },
    {
        "name": "The Case-Book of Sherlock Holmes",
        "url": "https://www.gutenberg.org/files/69700/69700-0.txt",
        "stories": [
            "The Adventure of the Illustrious Client",
            "The Adventure of the Blanched Soldier",
            "The Adventure of the Mazarin Stone",
            "The Adventure of the Three Gables",
            "The Adventure of the Sussex Vampire",
            "The Adventure of the Three Garridebs",
            "The Problem of Thor Bridge",
            "The Adventure of the Creeping Man",
            "The Adventure of the Lion's Mane",
            "The Adventure of the Veiled Lodger",
            "The Adventure of Shoscombe Old Place",
            "The Adventure of the Retired Colourman",
        ],
    },
]


# ─────────────────────────────────────────────────────────────────────────────
# Parsing — find where each story starts and ends within a collection
# ─────────────────────────────────────────────────────────────────────────────

def build_title_pattern(title: str, uppercase: bool = False) -> str:
    """
    Build a regex that matches a story title as a chapter heading.

    Handles several Gutenberg formatting conventions:
    - ALL-CAPS or title-case headings
    - Optional leading chapter keyword: "ADVENTURE ", "CHAPTER ", etc.
    - Optional roman-numeral / arabic-number prefix: "V.", "12."
    - Optional dash separator after numeral: "--", em-dash "—"
    - Typographic vs. ASCII quotes and apostrophes
    - Optional period/punctuation before a closing quotation mark
      (e.g. some editions print THE "GLORIA SCOTT." with a period before the
      closing quote — without this allowance the pattern fails entirely and
      the fallback keyword search picks up body-text mentions of the ship name)
    - Optional trailing period/punct at end of heading line
    """
    t = title.upper() if uppercase else title
    core = re.escape(t).replace(r"\ ", r"\s+")

    # Normalise apostrophes — re.escape() may or may not escape ' depending on
    # Python version (pre-3.7 → \', 3.7+ → bare '). \\?' handles both.
    core = re.sub(r"\\?'", "[\u2018\u2019']", core)

    # Normalise double quotes AND allow an optional Gutenberg italic marker
    # (_) or punctuation (. ! ,) immediately adjacent to each quote character.
    # Gutenberg plain-text files use _word_ for italics, so a story title
    # like The "Gloria Scott" appears in chapter headings as The "_Gloria Scott_"
    # — the underscore sits right after the opening " and right before the
    # closing ".  Without [.!,_]? and [_]? the pattern fails to match that
    # heading and the script picks up only the table-of-contents entry instead,
    # yielding a ~400-byte file containing just a few TOC lines.
    core = re.sub(r'\\?"', '[.!,_]?[\u201c\u201d"][_]?', core)

    # Normalise hyphens — some editions omit or vary hyphenation in compounds
    # (e.g. "STOCKBROKER" vs "STOCK-BROKER").
    core = re.sub(r"\\?-", lambda m: r"[\-\s]?", core)

    # Prefix: line boundary, optional all-caps chapter keyword (e.g. "ADVENTURE "),
    # optional numeral (e.g. "V." or "12."), optional dash separator ("--").
    # The [^A-Za-z0-9\n]{0,6} after the numeral flexibly absorbs "--", ".", " ", etc.
    prefix = (
        r"(?:^|\n)"               # line boundary
        r"\s*"                    # leading whitespace / indentation
        r"(?:[A-Z]+\s+)?"         # optional chapter keyword e.g. "ADVENTURE "
        r"(?:[IVXivx]+\.|[0-9]+\.)?"  # optional numeral e.g. "V." or "12."
        r"[^A-Za-z0-9\n]{0,6}"   # optional separator e.g. "--" or ".  "
    )

    # Suffix: optional trailing period/exclamation, optional subtitle
    # (": An Epilogue…"), then end-of-line.
    suffix = r"[.!]?\s*(?:[:\-\u2014][^\n]*)?\s*(?:\n|$)"

    return prefix + core + suffix


def _pick_match(matches, text, prefer_first=False):
    """
    Choose the regex match most likely to be a chapter heading.

    Filters out matches in the final 5 % of the text, which typically
    contains "Other Works by the Author" advertisements or appendices.
    For a story whose title equals the collection title (e.g. "His Last Bow")
    this prevents a book-list entry at the very end of the file from being
    selected as the chapter start, which would yield only a few hundred bytes.

    prefer_first  — when True, return the earliest surviving match (used for
                    the loose keyword fallback so we pick the chapter heading
                    rather than a later body-text mention of the same words).
    """
    cutoff = int(len(text) * 0.95)
    filtered = [m for m in matches if m.start() < cutoff]
    if not filtered:          # all matches in last 5 % — use unfiltered set
        filtered = matches
    if prefer_first:
        return filtered[0]
    return filtered[-1] if len(filtered) > 1 else filtered[0]


def split_collection(text: str, titles: list, collection_name: str):
    """Return [(title, story_text), ...] for each story found."""
    positions = []
    for title in titles:
        # First try: ALL-CAPS heading — matches actual story headings in
        # editions (e.g. Adventures, Return) where the TOC is title-case and
        # headings are ALL CAPS.
        pat_upper = build_title_pattern(title, uppercase=True)
        matches = list(re.finditer(pat_upper, text, re.MULTILINE))

        if not matches:
            # Second try: mixed-case with IGNORECASE — used for editions whose
            # headings are not ALL CAPS (e.g. Memoirs, His Last Bow, Case-Book).
            pat_mixed = build_title_pattern(title)
            matches = list(re.finditer(pat_mixed, text, re.IGNORECASE | re.MULTILINE))

        if matches:
            # Use _pick_match: takes the last match before the final 5% of the
            # file, so a story whose title also appears in an end-of-file book
            # list doesn't get its chapter start misidentified.
            m = _pick_match(matches, text, prefer_first=False)
            positions.append((m.start(), title, m.end()))
        else:
            # Looser fallback: key words in sequence.
            words_upper = [w for w in re.split(r"\W+", title.upper()) if len(w) > 3]
            # Use (?<![A-Za-z]) / (?![A-Za-z]) instead of \b so that
            # Gutenberg italic markers like _Gloria remain matchable —
            # Python treats _ as a word character so \b fails before _Gloria.
            loose_upper = (r"(?<![A-Za-z])" +
                           r"\W+".join(re.escape(w) for w in words_upper[:4]) +
                           r"(?![A-Za-z])")
            all_matches = list(re.finditer(loose_upper, text))
            if not all_matches:
                words = [w for w in re.split(r"\W+", title) if len(w) > 3]
                loose = (r"(?<![A-Za-z])" +
                         r"\W+".join(re.escape(w) for w in words[:4]) +
                         r"(?![A-Za-z])")
                all_matches = list(re.finditer(loose, text, re.IGNORECASE))
            if all_matches:
                # For the loose fallback take the FIRST match that falls after
                # the table of contents (first 5 % of the file) and before any
                # end-of-file appendix (last 5 %). This avoids both TOC hits
                # and book-list hits. It also prevents picking up in-story body
                # mentions that appear after the real chapter heading and would
                # leave only a tiny slice of text before the next chapter start.
                toc_end  = int(len(text) * 0.05)
                tail_start = int(len(text) * 0.95)
                mid = [m for m in all_matches if toc_end < m.start() < tail_start]
                m = mid[0] if mid else _pick_match(all_matches, text, prefer_first=True)
                line_start = text.rfind("\n", 0, m.start()) + 1
                positions.append((line_start, title, m.end()))
                print(f"    [loose match] '{title}'")
            else:
                print(f"    [WARNING] Could not locate: '{title}' — skipping")

    positions.sort(key=lambda x: x[0])

    results = []
    for i, (start, title, _) in enumerate(positions):
        end = positions[i + 1][0] if i + 1 < len(positions) else len(text)
        results.append((title, text[start:end]))

    return results


# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────

SMALL_FILE_THRESHOLD = 5_000   # bytes — warn if a saved story is suspiciously short

def main():
    total = 0
    warnings = []

    for collection in COLLECTIONS:
        cname = collection["name"]
        print(f"\n{'=' * 60}")
        print(f"  {cname}")
        print(f"{'=' * 60}")

        try:
            raw = fetch_text(collection["url"])
        except Exception as exc:
            print(f"  [ERROR] Could not download: {exc}")
            continue

        text = strip_gutenberg_boilerplate(raw)
        stories = split_collection(text, collection["stories"], cname)

        for title, body in stories:
            path = save_story(title, cname, body)
            size = os.path.getsize(path)
            words = len(body.split())
            flag = "  ← [WARNING: suspiciously small]" if size < SMALL_FILE_THRESHOLD else ""
            print(f"  ✓  {title}  ({words:,} words, {size:,} bytes){flag}")
            if flag:
                warnings.append(f"{title} ({size:,} bytes)")
            total += 1

        time.sleep(1)   # be polite to Gutenberg's servers

    print(f"\n{'=' * 60}")
    print(f"  Complete! {total} stories saved to:")
    print(f"  {OUTPUT_DIR}")
    print(f"{'=' * 60}\n")

    if warnings:
        print(f"WARNINGS — {len(warnings)} story file(s) are suspiciously small:")
        for w in warnings:
            print(f"  • {w}")
        print()

    # Final listing
    files = sorted(f for f in os.listdir(OUTPUT_DIR) if f.endswith(".txt"))
    print(f"Files ({len(files)}):")
    for fn in files:
        size = os.path.getsize(os.path.join(OUTPUT_DIR, fn))
        flag = "  ← small" if size < SMALL_FILE_THRESHOLD else ""
        print(f"  {fn}  ({size:,} bytes){flag}")


if __name__ == "__main__":
    main()
