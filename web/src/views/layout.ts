export function layout(title: string, content: string) {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${title} - Rust Server Admin</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <script>
    tailwind.config = {
      theme: {
        extend: {
          colors: {
            rust: { 50:'#fef3f2',100:'#fee4e2',200:'#fecdc9',400:'#f97066',500:'#cd412b',600:'#b5200b',700:'#9a1a0a',800:'#7f1a0e',900:'#6c1b13' }
          }
        }
      }
    }
  </script>
  <style>
    body { background: #111; }
    .console-output { font-family: 'Courier New', monospace; font-size: 13px; }
    .console-output::-webkit-scrollbar { width: 8px; }
    .console-output::-webkit-scrollbar-track { background: #1a1a1a; }
    .console-output::-webkit-scrollbar-thumb { background: #444; border-radius: 4px; }
  </style>
</head>
<body class="text-gray-200 min-h-screen">
  <nav class="bg-gray-900 border-b border-gray-800 px-6 py-3 flex items-center justify-between">
    <div class="flex items-center gap-4">
      <h1 class="text-lg font-bold text-rust-400">Rust Server Admin</h1>
      <a href="/dashboard" class="text-sm text-gray-400 hover:text-white">Dashboard</a>
      <a href="/rcon" class="text-sm text-gray-400 hover:text-white">RCON Console</a>
      <a href="/logs" class="text-sm text-gray-400 hover:text-white">Logs</a>
      <a href="/config" class="text-sm text-gray-400 hover:text-white">Config</a>
      <a href="/configs" class="text-sm text-gray-400 hover:text-white">Server Configs</a>
    </div>
    <form method="POST" action="/logout">
      <button class="text-sm text-gray-500 hover:text-red-400">Logout</button>
    </form>
  </nav>
  <main class="max-w-6xl mx-auto p-6">
    ${content}
  </main>
</body>
</html>`;
}
