const $ = selector => document.querySelector(selector);
const loginPanel = $('#loginPanel');
const appPanel = $('#appPanel');
const logoutButton = $('#logoutButton');
const fileList = $('#fileList');
const emptyState = $('#emptyState');
const uploadQueue = $('#uploadQueue');
let maxFileSize = 0;
let searchTimer;

async function api(url, options = {}) {
  const response = await fetch(url, options);
  if (response.status === 401) {
    showLogin();
    throw new Error('请先输入访问密码。');
  }
  if (response.status === 204) return null;
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || `请求失败（${response.status}）`);
  return body;
}

function showLogin() {
  loginPanel.classList.remove('hidden');
  appPanel.classList.add('hidden');
  logoutButton.classList.add('hidden');
  $('#password').focus();
}

function showApp() {
  loginPanel.classList.add('hidden');
  appPanel.classList.remove('hidden');
  logoutButton.classList.remove('hidden');
  loadFiles();
}

function formatSize(bytes) {
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let index = 0;
  while (value >= 1024 && index < units.length - 1) { value /= 1024; index++; }
  return `${value.toFixed(index ? 1 : 0)} ${units[index]}`;
}

function escapeHtml(value) {
  const node = document.createElement('span');
  node.textContent = value;
  return node.innerHTML;
}

function toast(message) {
  const element = $('#toast');
  element.textContent = message;
  element.classList.remove('hidden');
  clearTimeout(element.timer);
  element.timer = setTimeout(() => element.classList.add('hidden'), 3200);
}

async function loadFiles() {
  const query = encodeURIComponent($('#searchInput').value.trim());
  try {
    const files = await api(`/api/files?q=${query}`);
    $('#countText').textContent = `${files.length} 个文件`;
    emptyState.classList.toggle('hidden', files.length !== 0);
    fileList.innerHTML = files.map(file => `
      <article class="file-row">
        <div class="file-name">
          <strong title="${escapeHtml(file.originalName)}">${escapeHtml(file.originalName)}</strong>
          <span class="hash" title="SHA-256: ${file.sha256}">SHA-256 ${file.sha256.slice(0, 16)}…</span>
        </div>
        <span class="size muted">${file.sizeText}</span>
        <span class="date muted">${new Date(file.uploadedAt).toLocaleString()}</span>
        <div class="actions">
          <a href="/api/files/${file.id}/download">下载</a>
          <button class="danger" data-delete="${file.id}" data-name="${escapeHtml(file.originalName)}">删除</button>
        </div>
      </article>`).join('');
  } catch (error) { toast(error.message); }
}

function upload(file) {
  if (file.size > maxFileSize) { toast(`${file.name} 超过 ${formatSize(maxFileSize)} 限制`); return; }
  const row = document.createElement('div');
  row.className = 'upload-item';
  row.innerHTML = `<span class="name"></span><div class="progress"><span></span></div><span class="state">等待</span>`;
  row.querySelector('.name').textContent = file.name;
  uploadQueue.prepend(row);

  const data = new FormData();
  data.append('file', file);
  const xhr = new XMLHttpRequest();
  xhr.open('POST', '/api/files');
  xhr.setRequestHeader('X-FileShare-Request', '1');
  xhr.upload.onprogress = event => {
    if (!event.lengthComputable) return;
    const percent = Math.round(event.loaded / event.total * 100);
    row.querySelector('.progress span').style.width = `${percent}%`;
    row.querySelector('.state').textContent = `${percent}%`;
  };
  xhr.onload = () => {
    if (xhr.status >= 200 && xhr.status < 300) {
      row.querySelector('.state').textContent = '完成';
      loadFiles();
    } else {
      const body = (() => { try { return JSON.parse(xhr.responseText); } catch { return {}; } })();
      row.querySelector('.state').textContent = '失败';
      toast(body.error || `${file.name} 上传失败`);
      if (xhr.status === 401) showLogin();
    }
  };
  xhr.onerror = () => { row.querySelector('.state').textContent = '失败'; toast(`${file.name} 上传失败`); };
  xhr.send(data);
}

$('#loginForm').addEventListener('submit', async event => {
  event.preventDefault();
  $('#loginMessage').textContent = '';
  const body = new URLSearchParams({ password: $('#password').value });
  try {
    await api('/api/login', { method: 'POST', body });
    $('#password').value = '';
    showApp();
  } catch (error) { $('#loginMessage').textContent = error.message; }
});

logoutButton.addEventListener('click', async () => {
  await api('/api/logout', { method: 'POST', headers: { 'X-FileShare-Request': '1' } }).catch(() => {});
  showLogin();
});

$('#fileInput').addEventListener('change', event => {
  [...event.target.files].forEach(upload);
  event.target.value = '';
});

const dropZone = $('#dropZone');
['dragenter', 'dragover'].forEach(name => dropZone.addEventListener(name, event => { event.preventDefault(); dropZone.classList.add('dragging'); }));
['dragleave', 'drop'].forEach(name => dropZone.addEventListener(name, event => { event.preventDefault(); dropZone.classList.remove('dragging'); }));
dropZone.addEventListener('drop', event => [...event.dataTransfer.files].forEach(upload));

fileList.addEventListener('click', async event => {
  const button = event.target.closest('[data-delete]');
  if (!button || !confirm(`确定删除“${button.dataset.name}”？此操作无法撤销。`)) return;
  button.disabled = true;
  try {
    await api(`/api/files/${button.dataset.delete}`, { method: 'DELETE', headers: { 'X-FileShare-Request': '1' } });
    toast('文件已删除');
    await loadFiles();
  } catch (error) { toast(error.message); button.disabled = false; }
});

$('#searchInput').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(loadFiles, 250); });

(async () => {
  try {
    const status = await api('/api/status');
    maxFileSize = status.maxFileSizeBytes;
    $('#limitText').textContent = `单个文件最大 ${formatSize(maxFileSize)}`;
    status.authenticated ? showApp() : showLogin();
  } catch (error) { showLogin(); toast(error.message); }
})();
