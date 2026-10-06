#!/usr/bin/env bash
# Publishes wiki/*.md to the GitHub wiki (github.com/james-taplin/dads-decals/wiki).
# Converts repo-relative links/images to wiki form and skips image lines whose file
# doesn't exist yet (GUI screenshots still to come). Usage: tools/sync-wiki.sh [workdir]
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
work="${1:-$repo/dist/wiki-sync}"
raw="https://raw.githubusercontent.com/james-taplin/dads-decals/main"
blob="https://github.com/james-taplin/dads-decals/blob/main"

rm -rf "$work"
git clone -q https://github.com/james-taplin/dads-decals.wiki.git "$work"

for src in "$repo"/wiki/*.md; do
  name="$(basename "$src")"
  out="$work/$name"
  : > "$out"
  while IFS= read -r line || [ -n "$line" ]; do
    # Skip images that aren't in the repo yet.
    img_re='\(\.\./docs/images/([^)]+)\)'   # in a variable: inline regex quoting broke the match
    if [[ "$line" =~ $img_re ]]; then
      [ -f "$repo/docs/images/${BASH_REMATCH[1]}" ] || continue
    fi
    line="$(printf '%s' "$line" | sed -E \
      -e "s#\(\.\./docs/images/#(${raw}/docs/images/#g" \
      -e "s#\(\.\./([^)]+)\)#(${blob}/\1)#g" \
      -e 's#\(([A-Za-z0-9-]+)\.md(\#[^)]*)?\)#(\1\2)#g')"
    printf '%s\n' "$line" >> "$out"
  done < "$src"
done

cat > "$work/_Sidebar.md" <<'EOF'
**[Dad's Decals](Home)**

- [Installation](Installation)
- [Placing decals](Placing-Decals)
- [Editing decals](Editing-Decals)
- [Text and road numbers](Text-and-Road-Numbers)
- [Colour, finish and weathering](Colour-Finish-and-Weathering)
- [Layouts and sharing](Layouts-and-Sharing)
- [Your own images](Your-Own-Images)
- [FAQ and troubleshooting](FAQ-and-Troubleshooting)
- [How it works](How-It-Works)

[Source and downloads](https://github.com/james-taplin/dads-decals)
EOF

cd "$work"
git add -A
if git diff --cached --quiet; then echo "Wiki already up to date"; exit 0; fi
git commit -q -m "Sync wiki from repo wiki/ folder"
git push -q
echo "Wiki published: $(git log --oneline -1)"
