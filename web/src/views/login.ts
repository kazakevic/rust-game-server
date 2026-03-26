import { alert, button, input } from "./components";

export function loginPage(error?: string) {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Login - Rust GG Admin</title>
  <script src="https://unpkg.com/@tailwindcss/browser@4"></script>
  <style type="text/tailwindcss">
    @theme {
      --color-accent: #cd412b;
    }
  </style>
</head>
<body class="bg-zinc-50 text-zinc-900 min-h-screen flex items-center justify-center antialiased">
  <div class="w-full max-w-sm">
    <div class="text-center mb-8">
      <h1 class="text-2xl font-bold tracking-tight">Rust<span class="text-accent">GG</span></h1>
      <p class="text-sm text-zinc-500 mt-1">Server Administration</p>
    </div>
    <div class="rounded-xl border border-zinc-200 bg-white shadow-sm p-6">
      ${error ? `<div class="mb-4">${alert(error, "error")}</div>` : ""}
      <form method="POST" action="/login" class="space-y-4">
        ${input({ name: "username", label: "Username", type: "text", required: true, autocomplete: "username", placeholder: "Enter username" })}
        ${input({ name: "password", label: "Password", type: "password", required: true, autocomplete: "current-password", placeholder: "Enter password" })}
        <div class="pt-1">
          ${button("Sign in", { variant: "primary", size: "lg", type: "submit", class: "w-full" })}
        </div>
      </form>
    </div>
  </div>
</body>
</html>`;
}
