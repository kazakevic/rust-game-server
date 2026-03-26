import { layout } from "./layout";
import { pageHeader, alert, button, input, modal, emptyState, escapeHtml } from "./components";

interface ConfigsListData {
  files: string[];
  error?: string;
  success?: string;
}

interface ConfigsEditData {
  filename: string;
  content: string;
  error?: string;
  success?: string;
}

export function configsListPage(data: ConfigsListData) {
  const { files, error, success } = data;

  const banner = error
    ? `<div class="mb-6">${alert(escapeHtml(error), "error")}</div>`
    : success
    ? `<div class="mb-6">${alert(escapeHtml(success), "success")}</div>`
    : "";

  const fileRows = files.length
    ? files.map(f => `
        <a href="/configs/${encodeURIComponent(f)}"
           class="flex items-center justify-between rounded-lg border border-zinc-200 bg-white px-5 py-3.5 shadow-sm hover:border-zinc-300 hover:shadow transition-all group">
          <div class="flex items-center gap-3">
            <svg class="w-4.5 h-4.5 text-zinc-400 group-hover:text-zinc-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
            </svg>
            <span class="text-sm font-medium text-zinc-700 group-hover:text-zinc-900">${escapeHtml(f)}</span>
          </div>
          <svg class="w-4 h-4 text-zinc-300 group-hover:text-zinc-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
          </svg>
        </a>
      `).join("")
    : emptyState("No config files found in /cfg");

  const newFileModal = modal("new-file-modal", "Create New Config File", `
    <form method="POST" action="/api/configs/create">
      ${input({ name: "filename", placeholder: "filename.cfg", required: true })}
      <div class="flex gap-3 justify-end mt-4">
        ${button("Cancel", { variant: "outline", size: "sm", attrs: `onclick="document.getElementById('new-file-modal').classList.add('hidden')"` })}
        ${button("Create", { variant: "primary", size: "sm", type: "submit" })}
      </div>
    </form>
  `);

  return layout("Server Configs", `
    ${pageHeader("Server Configs", {
      description: "Browse and edit server configuration files",
      actions: `
        <form method="POST" action="/api/configs/reload">
          ${button("Reload Configs", { variant: "outline", size: "sm", type: "submit" })}
        </form>
        ${button("New File", { variant: "primary", size: "sm", attrs: `onclick="document.getElementById('new-file-modal').classList.remove('hidden')"` })}
      `,
    })}
    ${banner}
    <div class="space-y-2">
      ${fileRows}
    </div>
    ${newFileModal}
  `, { activePage: "configs" });
}

export function configsEditPage(data: ConfigsEditData) {
  const { filename, content, error, success } = data;

  const banner = error
    ? `<div class="mb-4">${alert(escapeHtml(error), "error")}</div>`
    : success
    ? `<div class="mb-4">${alert(escapeHtml(success), "success")}</div>`
    : "";

  return layout(`Edit ${filename}`, `
    <div class="flex items-center gap-3 mb-6">
      <a href="/configs" class="text-zinc-400 hover:text-zinc-600 transition-colors">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
        </svg>
      </a>
      <h1 class="text-2xl font-bold tracking-tight text-zinc-900">${escapeHtml(filename)}</h1>
    </div>
    ${banner}
    <form method="POST" action="/api/configs/${encodeURIComponent(filename)}/save" class="space-y-4">
      <div class="rounded-xl border border-zinc-200 bg-white shadow-sm overflow-hidden">
        <div class="relative">
          <textarea name="content" spellcheck="false"
                    class="w-full bg-zinc-950 text-zinc-300 font-mono text-sm leading-relaxed px-4 py-3 focus:outline-none resize-y"
                    style="min-height: 450px; tab-size: 4;"
                    >${escapeHtml(content)}</textarea>
          <div class="absolute top-2 right-3 text-xs text-zinc-600 font-mono" id="line-info"></div>
        </div>
      </div>
      <div class="flex items-center gap-3">
        ${button("Save", { variant: "primary", size: "md", type: "submit" })}
        <a href="/configs" class="inline-flex items-center justify-center rounded-lg h-9 px-4 text-sm font-medium border border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50 transition-colors">Cancel</a>
        ${button("Delete", {
          variant: "destructive",
          size: "md",
          class: "ml-auto",
          attrs: `onclick="if(confirm('Delete this file permanently?')){fetch('/api/configs/${encodeURIComponent(filename)}/delete',{method:'POST'}).then(()=>location.href='/configs')}"`,
        })}
      </div>
    </form>
    <script>
      const ta = document.querySelector('textarea[name="content"]');
      const info = document.getElementById('line-info');
      function updateInfo() {
        const val = ta.value.substring(0, ta.selectionStart);
        const line = val.split('\\n').length;
        const col = val.length - val.lastIndexOf('\\n');
        info.textContent = 'Ln ' + line + ', Col ' + col;
      }
      ta.addEventListener('click', updateInfo);
      ta.addEventListener('keyup', updateInfo);
      ta.addEventListener('input', updateInfo);
      updateInfo();
      ta.addEventListener('keydown', function(e) {
        if (e.key === 'Tab') {
          e.preventDefault();
          const start = this.selectionStart;
          const end = this.selectionEnd;
          this.value = this.value.substring(0, start) + '\\t' + this.value.substring(end);
          this.selectionStart = this.selectionEnd = start + 1;
        }
      });
      document.addEventListener('keydown', function(e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
          e.preventDefault();
          ta.closest('form').submit();
        }
      });
    </script>
  `, { activePage: "configs" });
}
