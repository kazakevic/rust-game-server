export function layout(title: string, content: string, opts?: { activePage?: string }) {
  const active = opts?.activePage || "";

  function navLink(href: string, label: string, page: string) {
    const isActive = active === page;
    const cls = isActive
      ? "text-zinc-900 font-medium"
      : "text-zinc-500 hover:text-zinc-900";
    return `<a href="${href}" class="text-sm transition-colors ${cls}">${label}</a>`;
  }

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${title} - Rust GG Admin</title>
  <script src="https://unpkg.com/@tailwindcss/browser@4"></script>
  <style type="text/tailwindcss">
    @theme {
      --color-primary: #18181b;
      --color-accent: #cd412b;
    }
    @keyframes ping {
      75%, 100% { transform: scale(2); opacity: 0; }
    }
    .animate-ping { animation: ping 1s cubic-bezier(0, 0, 0.2, 1) infinite; }
    .console-output {
      font-family: ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, monospace;
      font-size: 13px;
    }
    .console-output::-webkit-scrollbar { width: 6px; }
    .console-output::-webkit-scrollbar-track { background: #fafafa; border-radius: 3px; }
    .console-output::-webkit-scrollbar-thumb { background: #d4d4d8; border-radius: 3px; }
    .console-output::-webkit-scrollbar-thumb:hover { background: #a1a1aa; }
  </style>
</head>
<body class="bg-zinc-50 text-zinc-900 min-h-screen antialiased">
  <nav class="sticky top-0 z-40 border-b border-zinc-200 bg-white/80 backdrop-blur-lg">
    <div class="max-w-6xl mx-auto flex items-center justify-between h-14 px-6">
      <div class="flex items-center gap-8">
        <a href="/dashboard" class="text-base font-bold tracking-tight text-zinc-900">Rust<span class="text-accent">GG</span></a>
        <div class="flex items-center gap-5">
          ${navLink("/dashboard", "Dashboard", "dashboard")}
          ${navLink("/rcon", "Console", "rcon")}
          ${navLink("/logs", "Logs", "logs")}
          ${navLink("/config", "GunGame", "config")}
          ${navLink("/configs", "Server Configs", "configs")}
        </div>
      </div>
      <form method="POST" action="/logout">
        <button class="text-sm text-zinc-400 hover:text-red-500 transition-colors cursor-pointer">Logout</button>
      </form>
    </div>
  </nav>
  <main class="max-w-6xl mx-auto px-6 py-8">
    ${content}
  </main>
</body>
</html>`;
}
