// ─── Utility ────────────────────────────────────────────────────────────────

export function escapeHtml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

// ─── Badge ──────────────────────────────────────────────────────────────────

type BadgeVariant = "default" | "success" | "destructive" | "warning" | "outline";

const badgeStyles: Record<BadgeVariant, string> = {
  default: "bg-zinc-900 text-zinc-50",
  success: "bg-emerald-50 text-emerald-700 border border-emerald-200",
  destructive: "bg-red-50 text-red-700 border border-red-200",
  warning: "bg-amber-50 text-amber-700 border border-amber-200",
  outline: "border border-zinc-200 text-zinc-600",
};

export function badge(text: string, variant: BadgeVariant = "default"): string {
  return `<span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${badgeStyles[variant]}">${escapeHtml(text)}</span>`;
}

// ─── Alert / Banner ─────────────────────────────────────────────────────────

type AlertVariant = "error" | "success" | "warning" | "info";

const alertStyles: Record<AlertVariant, string> = {
  error: "bg-red-50 border-red-200 text-red-800",
  success: "bg-emerald-50 border-emerald-200 text-emerald-800",
  warning: "bg-amber-50 border-amber-200 text-amber-800",
  info: "bg-blue-50 border-blue-200 text-blue-800",
};

const alertIcons: Record<AlertVariant, string> = {
  error: `<svg class="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" stroke-width="2"/><path stroke-width="2" d="M12 8v4m0 4h.01"/></svg>`,
  success: `<svg class="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>`,
  warning: `<svg class="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86l-8.3 14.48A1 1 0 003 20h18a1 1 0 00.87-1.49l-8.3-14.48a1.04 1.04 0 00-1.74 0z"/></svg>`,
  info: `<svg class="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" stroke-width="2"/><path stroke-width="2" d="M12 16v-4m0-4h.01"/></svg>`,
};

export function alert(message: string, variant: AlertVariant = "info"): string {
  return `<div class="flex items-start gap-3 rounded-lg border px-4 py-3 text-sm ${alertStyles[variant]}">
    ${alertIcons[variant]}
    <span>${message}</span>
  </div>`;
}

// ─── Card ───────────────────────────────────────────────────────────────────

export function card(opts: { title?: string; description?: string; class?: string; headerRight?: string }, content: string): string {
  const header = opts.title
    ? `<div class="flex items-center justify-between border-b border-zinc-100 px-6 py-4">
        <div>
          <h3 class="text-base font-semibold text-zinc-900">${opts.title}</h3>
          ${opts.description ? `<p class="text-sm text-zinc-500 mt-0.5">${opts.description}</p>` : ""}
        </div>
        ${opts.headerRight || ""}
      </div>`
    : "";
  return `<div class="rounded-xl border border-zinc-200 bg-white shadow-sm ${opts.class || ""}">
    ${header}
    <div class="px-6 py-4">${content}</div>
  </div>`;
}

// ─── Stats Card ─────────────────────────────────────────────────────────────

export function statsCard(label: string, value: string, opts?: { icon?: string; detail?: string }): string {
  return `<div class="rounded-xl border border-zinc-200 bg-white shadow-sm px-5 py-4">
    <div class="flex items-center justify-between">
      <p class="text-sm font-medium text-zinc-500">${label}</p>
      ${opts?.icon || ""}
    </div>
    <p class="mt-1 text-2xl font-semibold tracking-tight text-zinc-900">${value}</p>
    ${opts?.detail ? `<p class="mt-1 text-xs text-zinc-400">${opts.detail}</p>` : ""}
  </div>`;
}

// ─── Button ─────────────────────────────────────────────────────────────────

type ButtonVariant = "primary" | "secondary" | "destructive" | "outline" | "ghost" | "success" | "warning";
type ButtonSize = "sm" | "md" | "lg";

const btnBase = "inline-flex items-center justify-center gap-2 rounded-lg font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-zinc-950 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 cursor-pointer";

const btnVariants: Record<ButtonVariant, string> = {
  primary: "bg-zinc-900 text-zinc-50 hover:bg-zinc-800",
  secondary: "bg-zinc-100 text-zinc-900 hover:bg-zinc-200",
  destructive: "bg-red-500 text-white hover:bg-red-600",
  outline: "border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50 hover:text-zinc-900",
  ghost: "text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900",
  success: "bg-emerald-600 text-white hover:bg-emerald-700",
  warning: "bg-amber-500 text-white hover:bg-amber-600",
};

const btnSizes: Record<ButtonSize, string> = {
  sm: "h-8 px-3 text-xs",
  md: "h-9 px-4 text-sm",
  lg: "h-10 px-6 text-sm",
};

export function button(text: string, opts?: {
  variant?: ButtonVariant;
  size?: ButtonSize;
  type?: string;
  class?: string;
  attrs?: string;
}): string {
  const variant = opts?.variant || "primary";
  const size = opts?.size || "md";
  const type = opts?.type || "button";
  return `<button type="${type}" class="${btnBase} ${btnVariants[variant]} ${btnSizes[size]} ${opts?.class || ""}" ${opts?.attrs || ""}>${text}</button>`;
}

// ─── Input ──────────────────────────────────────────────────────────────────

const inputBase = "flex h-9 w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm transition-colors placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent disabled:cursor-not-allowed disabled:opacity-50";

export function input(opts: {
  name: string;
  label?: string;
  type?: string;
  value?: string;
  placeholder?: string;
  required?: boolean;
  step?: string;
  autocomplete?: string;
  class?: string;
  hint?: string;
}): string {
  const type = opts.type || "text";
  const stepAttr = opts.step ? ` step="${opts.step}"` : type === "number" ? ' step="1"' : "";
  const reqAttr = opts.required ? " required" : "";
  const acAttr = opts.autocomplete ? ` autocomplete="${opts.autocomplete}"` : "";

  return `<div class="${opts.class || ""}">
    ${opts.label ? `<label class="block text-sm font-medium text-zinc-700 mb-1.5">${opts.label}</label>` : ""}
    <input type="${type}" name="${opts.name}" value="${escapeHtml(String(opts.value ?? ""))}"
           placeholder="${opts.placeholder || ""}"
           class="${inputBase}"${stepAttr}${reqAttr}${acAttr} />
    ${opts.hint ? `<p class="mt-1 text-xs text-zinc-400">${opts.hint}</p>` : ""}
  </div>`;
}

// ─── Checkbox ───────────────────────────────────────────────────────────────

export function checkbox(opts: {
  name: string;
  label: string;
  checked?: boolean;
  class?: string;
}): string {
  return `<div class="flex items-center gap-2.5 ${opts.class || ""}">
    <input type="hidden" name="${opts.name}" value="false">
    <input type="checkbox" name="${opts.name}" value="true" ${opts.checked ? "checked" : ""}
           class="h-4 w-4 rounded border-zinc-300 text-zinc-900 focus:ring-zinc-950 cursor-pointer" />
    <label class="text-sm text-zinc-600">${opts.label}</label>
  </div>`;
}

// ─── Select ─────────────────────────────────────────────────────────────────

export function select(opts: {
  id?: string;
  name?: string;
  items: { value: string; label: string; selected?: boolean }[];
  class?: string;
}): string {
  const options = opts.items
    .map(i => `<option value="${i.value}"${i.selected ? " selected" : ""}>${escapeHtml(i.label)}</option>`)
    .join("");
  return `<select${opts.id ? ` id="${opts.id}"` : ""}${opts.name ? ` name="${opts.name}"` : ""}
    class="flex h-9 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:border-transparent cursor-pointer ${opts.class || ""}">${options}</select>`;
}

// ─── Section ────────────────────────────────────────────────────────────────

export function section(title: string, content: string, opts?: { description?: string }): string {
  return `<div class="rounded-xl border border-zinc-200 bg-white shadow-sm">
    <div class="border-b border-zinc-100 px-6 py-4">
      <h3 class="text-base font-semibold text-zinc-900">${title}</h3>
      ${opts?.description ? `<p class="text-sm text-zinc-500 mt-0.5">${opts.description}</p>` : ""}
    </div>
    <div class="px-6 py-5">
      ${content}
    </div>
  </div>`;
}

// ─── Page Header ────────────────────────────────────────────────────────────

export function pageHeader(title: string, opts?: { description?: string; actions?: string }): string {
  return `<div class="flex items-center justify-between mb-6">
    <div>
      <h1 class="text-2xl font-bold tracking-tight text-zinc-900">${title}</h1>
      ${opts?.description ? `<p class="text-sm text-zinc-500 mt-1">${opts.description}</p>` : ""}
    </div>
    ${opts?.actions ? `<div class="flex items-center gap-2">${opts.actions}</div>` : ""}
  </div>`;
}

// ─── Status Dot ─────────────────────────────────────────────────────────────

export function statusDot(active: boolean): string {
  const color = active ? "bg-emerald-500" : "bg-red-400";
  const ring = active ? "ring-emerald-500/20" : "ring-red-400/20";
  return `<span class="relative flex h-2.5 w-2.5">
    ${active ? `<span class="absolute inline-flex h-full w-full animate-ping rounded-full ${color} opacity-75"></span>` : ""}
    <span class="relative inline-flex h-2.5 w-2.5 rounded-full ${color} ring-4 ${ring}"></span>
  </span>`;
}

// ─── Empty State ────────────────────────────────────────────────────────────

export function emptyState(message: string, opts?: { icon?: string }): string {
  const icon = opts?.icon || `<svg class="w-10 h-10 text-zinc-300" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-2.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"/></svg>`;
  return `<div class="flex flex-col items-center justify-center py-12 text-center">
    ${icon}
    <p class="mt-3 text-sm text-zinc-500">${message}</p>
  </div>`;
}

// ─── Modal ──────────────────────────────────────────────────────────────────

export function modal(id: string, title: string, content: string): string {
  return `<div id="${id}" class="hidden fixed inset-0 z-50">
    <div class="fixed inset-0 bg-zinc-950/50 backdrop-blur-sm" onclick="document.getElementById('${id}').classList.add('hidden')"></div>
    <div class="fixed inset-0 flex items-center justify-center p-4">
      <div class="relative w-full max-w-md rounded-xl border border-zinc-200 bg-white shadow-lg">
        <div class="border-b border-zinc-100 px-6 py-4">
          <h3 class="text-base font-semibold text-zinc-900">${title}</h3>
        </div>
        <div class="px-6 py-4">
          ${content}
        </div>
      </div>
    </div>
  </div>`;
}
