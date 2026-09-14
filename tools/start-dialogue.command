#!/bin/zsh
# Never enable shell tracing here: the server credential is private.
set +x
cd "${0:A:h}/../server" || exit 1
btd_health=$(/usr/bin/curl --silent --max-time 2 http://127.0.0.1:8082/health 2>/dev/null)
if [[ "$btd_health" == *'"service":"btd-dialogue"'* && "$btd_health" == *'"ready":true'* ]]; then
  print 'The dialogue server is already running and ready. Return to Unity.'
  exit 0
fi
if [[ -z "${OPENAI_API_KEY:-}" ]]; then
  OPENAI_API_KEY=$(/usr/bin/security find-generic-password -s 'BeforeTheDrop/OpenAI' -a 'openai' -w 2>/dev/null)
fi
if [[ -z "$OPENAI_API_KEY" ]]; then
  print 'No saved key found. See docs/LOCAL-SETUP.md to save it once in Keychain.'
  read -rs 'OPENAI_API_KEY?Or paste your key for this session, then press Enter (hidden): '
  print
fi
if [[ -z "$OPENAI_API_KEY" ]]; then
  print 'No key entered. Nothing started.'
  exit 1
fi
export OPENAI_API_KEY
exec node --watch src/dialogue.mjs
