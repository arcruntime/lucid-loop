const LOOPBACK_HOSTS = new Set(["127.0.0.1", "::1", "localhost"]);

function positiveInteger(value, fallback, name) {
  const parsed = value === undefined || value === "" ? fallback : Number(value);
  if (!Number.isSafeInteger(parsed) || parsed <= 0) throw new Error(`${name} must be a positive integer`);
  return parsed;
}

export function loadConfig(env = process.env) {
  const host = env.HOST || "127.0.0.1";
  const config = {
    host,
    port: positiveInteger(env.PORT, 8080, "PORT"),
    apiKey: env.OPENAI_API_KEY || "",
    accessToken: env.ACCESS_TOKEN || "",
    upstreamUrl: "wss://api.openai.com/v1/live/sessions",
    startTimeoutMs: positiveInteger(env.START_TIMEOUT_MS, 10_000, "START_TIMEOUT_MS"),
    startupTimeoutMs: positiveInteger(env.STARTUP_TIMEOUT_MS, 15_000, "STARTUP_TIMEOUT_MS"),
    closeTimeoutMs: positiveInteger(env.CLOSE_TIMEOUT_MS, 15_000, "CLOSE_TIMEOUT_MS"),
    maxDurationMs: positiveInteger(env.MAX_DURATION_MS, 10 * 60_000, "MAX_DURATION_MS"),
    maxSessions: positiveInteger(env.MAX_SESSIONS, 8, "MAX_SESSIONS"),
    maxPendingConnections: positiveInteger(env.MAX_PENDING_CONNECTIONS, 32, "MAX_PENDING_CONNECTIONS"),
    maxMessageBytes: positiveInteger(env.MAX_MESSAGE_BYTES, 256 * 1024, "MAX_MESSAGE_BYTES"),
    maxBufferedBytes: positiveInteger(env.MAX_BUFFERED_BYTES, 1024 * 1024, "MAX_BUFFERED_BYTES"),
  };
  validateConfig(config);
  return config;
}

export function validateConfig(config) {
  if (!LOOPBACK_HOSTS.has(config.host) && !config.accessToken) {
    throw new Error("ACCESS_TOKEN is required when HOST is not loopback");
  }
  return config;
}

export function isLoopbackHost(host) {
  return LOOPBACK_HOSTS.has(host);
}
