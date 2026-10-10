const form = document.querySelector('#media-form');
const separate = document.querySelector('#separate');
const aiOptions = document.querySelector('#ai-options');
const streamOptions = document.querySelector('#stream-options');
const trackField = document.querySelector('#track-field');
const audioTrack = document.querySelector('#audio-track');
const fileInput = document.querySelector('#media');
const filename = document.querySelector('#filename');
const message = document.querySelector('#message');
const files = document.querySelector('#files');
let probeId = 0;

separate.addEventListener('change', () => {
  aiOptions.classList.toggle('hidden', !separate.checked);
  streamOptions.disabled = separate.checked;
});

fileInput.addEventListener('change', async () => {
  const file = fileInput.files[0];
  filename.textContent = file?.name || 'No file selected';
  await loadAudioTracks(file);
});

async function loadAudioTracks(file) {
  const currentProbe = ++probeId;
  audioTrack.innerHTML = '';
  trackField.classList.add('hidden');
  if (!file) return;
  try {
    const body = new FormData();
    body.append('media', file);
    const response = await fetch('/api/probe', { method: 'POST', body });
    if (!response.ok || currentProbe !== probeId) return;
    const tracks = await response.json();
    if (tracks.length < 2) return;
    audioTrack.innerHTML = tracks.map(track => `<option value="${track.index}">${escapeHtml(trackLabel(track))}</option>`).join('');
    trackField.classList.remove('hidden');
  } catch { /* no track list: the first audio track is used */ }
}

function trackLabel(track) {
  const details = [track.language, track.title].filter(Boolean).join(' · ');
  return `Track ${track.index + 1}${details ? ' · ' + details : ''}`;
}

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = document.querySelector('#submit');
  button.disabled = true; message.textContent = 'Processing...'; message.className = 'message busy';
  try { const response = await fetch('/api/process', { method:'POST', body:new FormData(form) }); const data = await response.json(); if (!response.ok) throw new Error(data.error || 'Processing failed.'); message.textContent = data.output || 'Processing complete.'; message.className = 'message'; await loadResults(); } catch (error) { message.textContent = error.message; message.className = 'message busy'; } finally { button.disabled = false; }
});
async function loadResults() { const response = await fetch('/api/results'); const entries = await response.json(); files.innerHTML = entries.length ? entries.map(file => `<div class="file"><span>${escapeHtml(file.name)}</span><a href="${file.url}" download>Download ↓</a></div>`).join('') : ''; }
function escapeHtml(value) { return value.replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[char])); }
document.querySelector('#refresh').addEventListener('click', loadResults); loadResults();
