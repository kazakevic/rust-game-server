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
