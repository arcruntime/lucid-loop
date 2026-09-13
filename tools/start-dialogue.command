#!/bin/zsh
cd "${0:A:h}/../server" || exit 1
if [[ -z "${OPENAI_API_KEY:-}" ]]; then
  read -rs 'OPENAI_API_KEY?Paste your OpenAI API key, then press Enter (hidden): '
  print
  export OPENAI_API_KEY
fi
if [[ -z "$OPENAI_API_KEY" ]]; then
  print 'No key entered. Nothing started.'
  exit 1
fi
exec node src/dialogue.mjs
