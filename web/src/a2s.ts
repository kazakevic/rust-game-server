import dgram from "node:dgram";

// Minimal A2S_INFO (Steam "Source Engine Query") client. This is exactly what the in-game
// server browser, BattleMetrics, and the Steam master server send to a server's query port
// to discover it. If the query port answers this, the server is ready to be listed; if it
// does NOT, the server is invisible in the browser no matter how healthy it otherwise is.
//
// Note: we query from inside the VPS (web-admin -> rust-server over the Docker network), so
// this proves the *server* is answering. It cannot detect an external firewall blocking the
// port from the public internet — a packet to our own public IP loops back without hitting
// the edge firewall. So: answering=true here + "not in the browser" => firewall problem.

export interface A2SInfo {
  answering: boolean;
  name?: string;
  map?: string;
  players?: number;
  maxPlayers?: number;
  error?: string;
}

const HEADER = Buffer.from([0xff, 0xff, 0xff, 0xff]);
const A2S_INFO_PAYLOAD = Buffer.from("Source Engine Query\0", "latin1");
const A2S_INFO_REQUEST = Buffer.concat([HEADER, Buffer.from([0x54]), A2S_INFO_PAYLOAD]);

function readCString(buf: Buffer, offset: number): [string, number] {
  let end = offset;
  while (end < buf.length && buf[end] !== 0) end++;
  return [buf.subarray(offset, end).toString("utf-8"), end + 1];
}

// Parse a 0x49 ('I') A2S_INFO response into the fields we surface on the dashboard.
function parseInfo(msg: Buffer): A2SInfo {
  try {
    let o = 5; // skip 4-byte 0xFFFFFFFF header + 1-byte type ('I')
    o += 1; // protocol byte
    let name: string, map: string, folder: string, game: string;
    [name, o] = readCString(msg, o);
    [map, o] = readCString(msg, o);
    [folder, o] = readCString(msg, o);
    [game, o] = readCString(msg, o);
    o += 2; // app id (int16)
    const players = msg[o]; o += 1;
    const maxPlayers = msg[o]; o += 1;
    return { answering: true, name, map, players, maxPlayers };
  } catch {
    // It answered (that's what matters for visibility); we just couldn't fully parse it.
    return { answering: true };
  }
}

// Send an A2S_INFO query and resolve with the parsed result. Never rejects — a failure to
// reach/parse resolves to { answering: false, error } so callers can render it directly.
export function queryA2SInfo(host: string, port: number, timeoutMs = 2500): Promise<A2SInfo> {
  return new Promise((resolve) => {
    const sock = dgram.createSocket("udp4");
    let settled = false;

    const finish = (r: A2SInfo) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      try { sock.close(); } catch {}
      resolve(r);
    };

    const timer = setTimeout(() => finish({ answering: false, error: "timeout" }), timeoutMs);

    const send = (challenge?: Buffer) => {
      const pkt = challenge ? Buffer.concat([A2S_INFO_REQUEST, challenge]) : A2S_INFO_REQUEST;
      sock.send(pkt, port, host, (err) => {
        if (err) finish({ answering: false, error: err.message });
      });
    };

    sock.on("error", (err) => finish({ answering: false, error: err.message }));
    sock.on("message", (msg) => {
      if (msg.length < 5) return;
      const type = msg[4];
      if (type === 0x41) {
        // Challenge response — resend the request with the 4-byte challenge appended.
        send(msg.subarray(5, 9));
        return;
      }
      if (type === 0x49) finish(parseInfo(msg));
    });

    send();
  });
}
