export function loginPage(error?: string) {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Login - Rust Server Admin</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <style>body { background: #111; }</style>
</head>
<body class="text-gray-200 min-h-screen flex items-center justify-center">
  <div class="bg-gray-900 border border-gray-800 rounded-lg p-8 w-full max-w-sm">
    <h1 class="text-xl font-bold text-center mb-6">Rust Server Admin</h1>
    ${error ? `<div class="bg-red-900/50 border border-red-700 text-red-300 text-sm rounded p-3 mb-4">${error}</div>` : ""}
    <form method="POST" action="/login" class="space-y-4">
      <div>
        <label class="block text-sm text-gray-400 mb-1">Username</label>
        <input name="username" type="text" required autocomplete="username"
          class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-sm focus:outline-none focus:border-orange-500" />
      </div>
      <div>
        <label class="block text-sm text-gray-400 mb-1">Password</label>
        <input name="password" type="password" required autocomplete="current-password"
          class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-sm focus:outline-none focus:border-orange-500" />
      </div>
      <button type="submit"
        class="w-full bg-rust-600 hover:bg-rust-700 text-white font-medium rounded px-4 py-2 text-sm">
        Sign In
      </button>
    </form>
  </div>
</body>
</html>`;
}
