import Docker from "dockerode";

const docker = new Docker({ socketPath: "/var/run/docker.sock" });

const CONTAINER_NAME = process.env.RUST_CONTAINER_NAME || "rust-server";

async function getContainer() {
  return docker.getContainer(CONTAINER_NAME);
}

export async function getServerStatus() {
  try {
    const container = await getContainer();
    const info = await container.inspect();
    return {
      running: info.State.Running,
      status: info.State.Status,
      startedAt: info.State.StartedAt,
      health: info.State.Health?.Status || "N/A",
    };
  } catch {
    return { running: false, status: "not found", startedAt: "", health: "N/A" };
  }
}

export async function getServerStats() {
  try {
    const container = await getContainer();
    const stats = await container.stats({ stream: false });

    const cpuDelta = stats.cpu_stats.cpu_usage.total_usage - stats.precpu_stats.cpu_usage.total_usage;
    const sysDelta = stats.cpu_stats.system_cpu_usage - stats.precpu_stats.system_cpu_usage;
    const cpuCount = stats.cpu_stats.online_cpus || 1;
    const cpuPercent = sysDelta > 0 ? (cpuDelta / sysDelta) * cpuCount * 100 : 0;

    const memUsage = stats.memory_stats.usage || 0;
    const memLimit = stats.memory_stats.limit || 1;

    return {
      cpu: cpuPercent.toFixed(1),
      memoryUsed: (memUsage / 1024 / 1024).toFixed(0),
      memoryLimit: (memLimit / 1024 / 1024).toFixed(0),
      memoryPercent: ((memUsage / memLimit) * 100).toFixed(1),
    };
  } catch {
    return { cpu: "0", memoryUsed: "0", memoryLimit: "0", memoryPercent: "0" };
  }
}

export async function restartServer() {
  const container = await getContainer();
  await container.restart();
}

export async function stopServer() {
  const container = await getContainer();
  await container.stop();
}

export async function startServer() {
  const container = await getContainer();
  await container.start();
}

export async function execInServer(cmd: string[]): Promise<string> {
  const container = await getContainer();
  const exec = await container.exec({ Cmd: cmd, AttachStdout: true, AttachStderr: true });

  return new Promise((resolve, reject) => {
    exec.start({ hijack: true, stdin: false }, (err: any, stream: any) => {
      if (err) return reject(err);
      const chunks: Buffer[] = [];
      stream.on("data", (chunk: Buffer) => chunks.push(chunk));
      stream.on("end", () => {
        const raw = Buffer.concat(chunks);
        // Strip Docker stream headers (8-byte prefix per frame)
        const lines: string[] = [];
        let offset = 0;
        while (offset < raw.length) {
          if (offset + 8 > raw.length) break;
          const size = raw.readUInt32BE(offset + 4);
          if (offset + 8 + size > raw.length) {
            lines.push(raw.subarray(offset + 8).toString("utf-8"));
            break;
          }
          lines.push(raw.subarray(offset + 8, offset + 8 + size).toString("utf-8"));
          offset += 8 + size;
        }
        resolve(lines.join(""));
      });
      stream.on("error", reject);
    });
  });
}

export async function getServerLogs(tail: number = 200, since?: number): Promise<string> {
  try {
    const container = await getContainer();
    const opts: any = { stdout: true, stderr: true, tail, timestamps: true };
    if (since) opts.since = since;
    const stream = await container.logs(opts);
    // dockerode returns a Buffer; strip Docker stream headers (8-byte prefix per frame)
    const raw = Buffer.isBuffer(stream) ? stream : Buffer.from(stream as any);
    const lines: string[] = [];
    let offset = 0;
    while (offset < raw.length) {
      if (offset + 8 > raw.length) break;
      const size = raw.readUInt32BE(offset + 4);
      if (offset + 8 + size > raw.length) {
        // partial frame — take what we can
        lines.push(raw.subarray(offset + 8).toString("utf-8"));
        break;
      }
      lines.push(raw.subarray(offset + 8, offset + 8 + size).toString("utf-8"));
      offset += 8 + size;
    }
    return lines.join("").replace(/\r/g, "");
  } catch {
    return "(unable to fetch logs — is the container running?)";
  }
}
