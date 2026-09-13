import { loadConfig } from "./config.mjs";
import { createRelayServer } from "./server.mjs";
import { createGameSessions } from "./game-sessions.mjs";
import { createDemoDefinition, createDemoScenario } from "./demo-scenario.mjs";
import { createEncounterWorld } from "./encounter-world.mjs";

try {
  const config = loadConfig();
  const gameSessions = createGameSessions({ definitionFactory: createDemoDefinition, encounterFactory: createDemoScenario });
  const relay = createRelayServer({ ...config, gameSessions, worldFactory: createEncounterWorld });
  await relay.listen();
  console.log(`Lucid Loop live relay listening on ${config.host}:${config.port}`);
  const shutdown = async () => { await relay.close(); process.exit(0); };
  process.once("SIGINT", shutdown);
  process.once("SIGTERM", shutdown);
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
