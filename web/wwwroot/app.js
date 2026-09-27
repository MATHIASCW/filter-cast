const form = document.querySelector('#media-form');
const separate = document.querySelector('#separate');
const aiOptions = document.querySelector('#ai-options');
const fileInput = document.querySelector('#media');
const filename = document.querySelector('#filename');
const message = document.querySelector('#message');
const files = document.querySelector('#files');
separate.addEventListener('change', () => aiOptions.classList.toggle('hidden', !separate.checked));
fileInput.addEventListener('change', () => { filename.textContent = fileInput.files[0]?.name || 'No file selected'; });
form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = document.querySelector('#submit');
  button.disabled = true; message.textContent = 'Processing...'; message.className = 'message busy';
  try { const response = await fetch('/api/process', { method:'POST', body:new FormData(form) }); const data = await response.json(); if (!response.ok) throw new Error(data.error || 'Processing failed.'); message.textContent = data.output || 'Processing complete.'; message.className = 'message'; await loadResults(); } catch (error) { message.textContent = error.message; message.className = 'message busy'; } finally { button.disabled = false; }
});
async function loadResults() { const response = await fetch('/api/results'); const entries = await response.json(); files.innerHTML = entries.length ? entries.map(file => `<div class="file"><span>${escapeHtml(file.name)}</span><a href="${file.url}" download>Download ↓</a></div>`).join('') : ''; }
function escapeHtml(value) { return value.replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[char])); }
document.querySelector('#refresh').addEventListener('click', loadResults); loadResults();