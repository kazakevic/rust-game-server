export class RconClient {
  private ws: WebSocket | null = null;
  private requestId = 0;
  private pending = new Map<number, { resolve: (v: string) => void; reject: (e: Error) => void }>();

  constructor(
    private host: string,
    private port: number,
    private password: string
  ) {}

  private async connect(): Promise<WebSocket> {
    if (this.ws?.readyState === WebSocket.OPEN) return this.ws;

    return new Promise((resolve, reject) => {
      const ws = new WebSocket(`ws://${this.host}:${this.port}/${this.password}`);
      const timeout = setTimeout(() => {
        ws.close();
        reject(new Error("RCON connection timeout"));
      }, 5000);

      ws.onopen = () => {
        clearTimeout(timeout);
        this.ws = ws;
        resolve(ws);
      };

      ws.onmessage = (event) => {
        try {
          const data = JSON.parse(String(event.data));
          const pending = this.pending.get(data.Identifier);
          if (pending) {
            this.pending.delete(data.Identifier);
            pending.resolve(data.Message || "");
          }
        } catch {}
      };

      ws.onerror = (err) => {
        clearTimeout(timeout);
        reject(new Error("RCON connection failed"));
      };

      ws.onclose = () => {
        this.ws = null;
      };
    });
  }

  async command(cmd: string): Promise<string> {
    const ws = await this.connect();
    const id = ++this.requestId;

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error("RCON command timeout"));
      }, 10000);

      this.pending.set(id, {
        resolve: (msg) => {
          clearTimeout(timeout);
          resolve(msg);
        },
        reject: (err) => {
          clearTimeout(timeout);
          reject(err);
        },
      });

      ws.send(
        JSON.stringify({
          Identifier: id,
          Message: cmd,
          Name: "WebAdmin",
        })
      );
    });
  }

  disconnect() {
    this.ws?.close();
    this.ws = null;
  }
}
