#!/bin/sh
# Thin universal macOS .dylib files to a single architecture.
# Usage: thin-macos-natives.sh <lipo-tool> <arch> <output-dir> <dylib...>
set -eu

LIPO="${1:?lipo tool required}"
ARCH="${2:?architecture required}"
THIN_DIR="${3:?output directory required}"
shift 3

mkdir -p "$THIN_DIR"
thinned=0

for SRC in "$@"; do
  [ -f "$SRC" ] || continue
  ARCHS="$("$LIPO" -archs "$SRC" 2>/dev/null || true)"
  case " $ARCHS " in
    *" $ARCH "*) ;;
    *)
      echo "PolyInstall: skip $(basename "$SRC") — does not contain $ARCH (archs: $ARCHS)"
      continue
      ;;
  esac

  COUNT="$(printf '%s' "$ARCHS" | wc -w | tr -d ' ')"
  BASE="$(basename "$SRC")"
  OUT="$THIN_DIR/$BASE"

  if [ "$COUNT" -le 1 ]; then
    echo "PolyInstall: already thin $BASE ($ARCHS)"
    cp -f "$SRC" "$OUT"
  else
    "$LIPO" "$SRC" -thin "$ARCH" -output "$OUT"
    echo "PolyInstall: thinned $BASE ($ARCHS → $ARCH)"
    thinned=$((thinned + 1))
  fi
done

echo "PolyInstall: thinned $thinned macOS native librar(ies) for $ARCH"
