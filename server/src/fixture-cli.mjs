import { createFixtureServer } from "./fixture.mjs";

const host = process.env.FIXTURE_HOST || "127.0.0.1";
const port = Number(process.env.FIXTURE_PORT || 8081);
const fixture = createFixtureServer({ host, port });
await fixture.listen();
console.log(`Lucid Loop protocol fixture listening on ws://${host}:${port}/live (no OpenAI connection)`);
const shutdown = async () => { await fixture.close(); process.exit(0); };
process.once("SIGINT", shutdown);
process.once("SIGTERM", shutdown);
