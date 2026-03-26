import { layout } from "./layout";

interface ConfigsListData {
  files: string[];
  error?: string;
}

interface ConfigsEditData {
  filename: string;
  content: string;
  error?: string;
  success?: string;
}

export function configsListPage(data: ConfigsListData) {
  const { files, error } = data;

  const banner = error
    ? `<div class="bg-red-900/50 border border-red-700 text-red-300 px-4 py-3 rounded mb-6">${escapeHtml(error)}</div>`
    : "";

  const fileRows = files.length
    ? files.map(f => `
        <a href="/configs/${encodeURIComponent(f)}"
           class="flex items-center justify-between bg-gray-900 border border-gray-800 rounded-lg px-5 py-4 hover:border-gray-600 transition-colors group">
          <div class="flex items-center gap-3">
            <svg class="w-5 h-5 text-gray-500 group-hover:text-rust-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
            </svg>
            <span class="text-gray-200 font-medium">${escapeHtml(f)}</span>
          </div>
          <svg class="w-4 h-4 text-gray-600 group-hover:text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
          </svg>
        </a>
      `).join("")
    : `<div class="bg-gray-900 border border-gray-800 rounded-lg px-5 py-8 text-center text-gray-500">No config files found in /cfg</div>`;

  return layout("Server Configs", `
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-2xl font-bold">Server Configs</h2>
      <button onclick="document.getElementById('new-file-modal').classList.remove('hidden')"
              class="bg-rust-600 hover:bg-rust-700 text-white px-4 py-2 rounded text-sm font-medium">
        New File
      </button>
    </div>
    ${banner}
    <div class="space-y-2">
      ${fileRows}
    </div>

    <!-- New file modal -->
    <div id="new-file-modal" class="hidden fixed inset-0 bg-black/60 flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-lg p-6 w-full max-w-md">
        <h3 class="text-lg font-semibold mb-4">Create New Config File</h3>
        <form method="POST" action="/api/configs/create">
          <input type="text" name="filename" placeholder="filename.cfg" required
                 pattern="[a-zA-Z0-9_\\-]+\\.[a-zA-Z0-9]+"
                 class="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-gray-200 focus:outline-none focus:border-rust-400 mb-4">
          <div class="flex gap-3 justify-end">
            <button type="button" onclick="document.getElementById('new-file-modal').classList.add('hidden')"
                    class="bg-gray-700 hover:bg-gray-600 text-white px-4 py-2 rounded text-sm">Cancel</button>
            <button type="submit"
                    class="bg-rust-600 hover:bg-rust-700 text-white px-4 py-2 rounded text-sm font-medium">Create</button>
          </div>
        </form>
      </div>
    </div>
  `);
}

export function configsEditPage(data: ConfigsEditData) {
  const { filename, content, error, success } = data;

  const banner = error
    ? `<div class="bg-red-900/50 border border-red-700 text-red-300 px-4 py-3 rounded mb-4">${escapeHtml(error)}</div>`
    : success
    ? `<div class="bg-green-900/50 border border-green-700 text-green-300 px-4 py-3 rounded mb-4">${escapeHtml(success)}</div>`
    : "";

  return layout(`Edit ${filename}`, `
    <div class="flex items-center gap-3 mb-6">
      <a href="/configs" class="text-gray-500 hover:text-gray-300">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
        </svg>
      </a>
      <h2 class="text-2xl font-bold">${escapeHtml(filename)}</h2>
    </div>
    ${banner}
    <form method="POST" action="/api/configs/${encodeURIComponent(filename)}/save" class="space-y-4">
      <div class="relative">
        <textarea name="content" spellcheck="false"
                  class="w-full bg-gray-900 border border-gray-800 rounded-lg px-4 py-3 text-gray-200 font-mono text-sm leading-relaxed focus:outline-none focus:border-rust-400 resize-y"
                  style="min-height: 400px; tab-size: 4;"
                  >${escapeHtml(content)}</textarea>
        <div class="absolute top-2 right-2 text-xs text-gray-600" id="line-info"></div>
      </div>
      <div class="flex items-center gap-3">
        <button type="submit" class="bg-green-600 hover:bg-green-700 text-white px-6 py-2 rounded font-medium text-sm">
          Save
        </button>
        <a href="/configs" class="bg-gray-700 hover:bg-gray-600 text-white px-6 py-2 rounded font-medium text-sm">Cancel</a>
        <button type="button" onclick="if(confirm('Delete this file permanently?')) { fetch('/api/configs/${encodeURIComponent(filename)}/delete', {method:'POST'}).then(()=>location.href='/configs') }"
                class="ml-auto bg-red-900/50 hover:bg-red-800 text-red-400 hover:text-red-300 border border-red-800 px-4 py-2 rounded text-sm">
          Delete
        </button>
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
      // Tab key inserts tab instead of moving focus
      ta.addEventListener('keydown', function(e) {
        if (e.key === 'Tab') {
          e.preventDefault();
          const start = this.selectionStart;
          const end = this.selectionEnd;
          this.value = this.value.substring(0, start) + '\\t' + this.value.substring(end);
          this.selectionStart = this.selectionEnd = start + 1;
        }
      });
      // Ctrl+S / Cmd+S to save
      document.addEventListener('keydown', function(e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
          e.preventDefault();
          ta.closest('form').submit();
        }
      });
    </script>
  `);
}

function escapeHtml(s: string) {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
