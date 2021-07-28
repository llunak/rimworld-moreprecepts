#! /bin/sh
git status --porcelain
#wait for keypress or Ctrl+C
read

git clean -idx .
rm -rf .git
rm -f clean.sh
