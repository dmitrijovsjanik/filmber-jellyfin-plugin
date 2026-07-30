#!/usr/bin/env python3
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
movie = root / "Movies" / "Spike Movie (1999)" / "Spike Movie (1999).nfo"
series = root / "Shows" / "Spike Series" / "tvshow.nfo"
episode = root / "Shows" / "Spike Series" / "Season 01" / "Spike Series S01E01.nfo"

movie.write_text(
    """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<movie>
  <title>Spike Movie</title>
  <year>1999</year>
  <tmdbid>603</tmdbid>
  <imdbid>tt0133093</imdbid>
  <uniqueid type="tmdb" default="true">603</uniqueid>
  <uniqueid type="imdb">tt0133093</uniqueid>
</movie>
""",
    encoding="utf-8",
)
series.write_text(
    """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<tvshow>
  <title>Spike Series</title>
  <year>2011</year>
  <tmdbid>1399</tmdbid>
  <uniqueid type="tmdb" default="true">1399</uniqueid>
</tvshow>
""",
    encoding="utf-8",
)
episode.write_text(
    """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<episodedetails>
  <title>Spike Episode</title>
  <season>1</season>
  <episode>1</episode>
</episodedetails>
""",
    encoding="utf-8",
)
