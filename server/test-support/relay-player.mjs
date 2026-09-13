import assert from 'node:assert/strict';
import WebSocket from 'ws';
import { setTimeout as delay } from 'node:timers/promises';

// A player drives only public WebSocket messages. It never mutates the registry,
// fabricates world observations, teleports, or bypasses conversation proximity.
export class RelayPlayer {
  constructor(address, signal) {
    this.address = address; this.signal = signal; this.sequence = 0; this.request = 0;
    this.channels = new Set(); this.state = null; this.world = null;
  }

  async until(check, label, seconds = 35) {
    const deadline = Date.now() + seconds * 1000;
    while (!check()) {
      this.signal?.throwIfAborted();
      for (const channel of this.channels) if (channel.error) throw channel.error;
      assert.ok(Date.now() < deadline, `Timed out: ${label}; phase=${this.state?.phase}`);
      await delay(20, undefined, { signal: this.signal });
    }
  }

  async open(path) {
    const channel = { socket: new WebSocket(this.address + path), events: [], error: null };
    this.channels.add(channel);
    channel.socket.on('error', () => { channel.error = new Error('Relay WebSocket error'); });
    channel.socket.on('message', raw => {
      const event = JSON.parse(raw);
      if (event.type === 'game.ready' || event.type === 'game.state') this.state = event.snapshot;
      if (event.type === 'game.world') this.world = event.world;
      if (event.type === 'game.error' || (event.type === 'gym.status' && event.status === 'error'))
        channel.error = new Error(`Relay rejected request: ${event.code ?? 'live_error'}`);
      // Never retain continuous PCM or world frames in a test mailbox.
      if (['game.ready', 'game.move_result', 'game.intent_result', 'gym.status', 'session.closed'].includes(event.type)) {
        channel.events.push(event);
        if (channel.events.length > 64) channel.events.shift();
      }
    });
    await this.until(() => channel.socket.readyState === WebSocket.OPEN, 'WebSocket open', 10);
    return channel;
  }

  send(channel, value) {
    assert.equal(channel.socket.readyState, WebSocket.OPEN, 'Player socket must be open');
    channel.socket.send(JSON.stringify(value));
  }

  async receive(channel, type, predicate = () => true, seconds = 35) {
    let index = -1;
    await this.until(() => (index = channel.events.findIndex(event => event.type === type && predicate(event))) >= 0,
      type, seconds);
    return channel.events.splice(index, 1)[0];
  }

  async start() {
    this.game = await this.open('/game');
    this.send(this.game, { type: 'game.create' });
    const ready = await this.receive(this.game, 'game.ready');
    assert.equal(ready.ok, true);
    this.credentials = ready.credentials;
    await this.until(() => this.world?.loopId === this.state.loopId, 'initial world');
  }

  async walk(x, z, seconds = 35) {
    this.send(this.game, { type: 'game.walk', loopId: this.state.loopId,
      sequence: ++this.sequence, destination: { x, z } });
    assert.equal((await this.receive(this.game, 'game.move_result')).accepted, true, 'Walk must be accepted');
    await this.until(() => {
      const player = this.world?.actors.player;
      return player && Math.hypot(player.position.x - x, player.position.z - z) < .12;
    }, `walk to ${x},${z}`, seconds);
  }

  async say(npcId, text) {
    const voice = await this.open('/live');
    let audioTimer;
    try {
      this.send(voice, { type: 'gym.start', character: npcId, ...this.credentials });
      await this.receive(voice, 'gym.status', event => event.status === 'ready');
      const silence = Buffer.alloc(2400 * 2).toString('base64');
      audioTimer = setInterval(() => {
        if (voice.socket.readyState === WebSocket.OPEN)
          this.send(voice, { type: 'session.input_audio.append', audio: silence });
      }, 100);
      const results = [];
      for (const utterance of Array.isArray(text) ? text : [text]) {
        const requestId = `prevention:${++this.request}`;
        this.send(voice, { type: 'game.text', requestId, text: utterance });
        const result = await this.receive(voice, 'game.intent_result', event => event.requestId === requestId);
        assert.equal(result.ok, true, `Intent failed for ${npcId}: ${result.reason}`);
        assert.equal(result.committed, true, `Action rejected for ${npcId}: ${result.outcome?.reason ?? result.kind}`);
        results.push(result);
      }
      clearInterval(audioTimer);
      this.send(voice, { type: 'session.close' });
      const closed = await this.receive(voice, 'session.closed', () => true, 20);
      assert.ok(closed.usage, 'Real Live session must return final usage');
      return results;
    } finally {
      clearInterval(audioTimer); voice.socket.terminate(); this.channels.delete(voice);
    }
  }

  close() {
    for (const channel of this.channels) channel.socket.terminate();
    this.channels.clear();
  }
}
