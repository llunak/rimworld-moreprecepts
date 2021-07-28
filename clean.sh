#! /bin/sh
echo git status:
git status --porcelain
#wait for keypress or Ctrl+C
read

git clean -idx .
rm -rf .git
find . -type f -name '.git*' -print0 | xargs -0 rm -f
rm -f clean.sh
