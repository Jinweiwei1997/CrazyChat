// Bakes the check-in success Lottie into the sprite sheet read by OverlayTodoUi.
// Runtime stays asset-only: no Lottie player ships with the game.
//
//   node BakeCheckinAnimation.cjs <Success.json> <lottie.min.js>
//
// Renders with headless Edge (canvas renderer keeps the alpha channel) and
// composites 90 frames into a 10x9 sheet of 64px cells.

const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

const CELL = 64;
const COLS = 10;
const ROWS = 9;
const FRAMES = 90;

const source = process.argv[2] || 'd:/Download/Success.json';
const player = process.argv[3] || path.join(__dirname, 'lottie.min.js');
const output = path.resolve(__dirname, '../../../Resources/Overlay/UI/checkin_success_sheet.png');
const edge = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';

const work = fs.mkdtempSync(path.join(os.tmpdir(), 'crazychat-bake-'));
const page = path.join(work, 'bake.html');

fs.writeFileSync(page, `<!doctype html><meta charset="utf-8">
<body style="margin:0;background:transparent">
<div id="stage" style="width:${CELL}px;height:${CELL}px"></div>
<script>${fs.readFileSync(player, 'utf8')}</script>
<script>
const anim = lottie.loadAnimation({
  container: document.getElementById('stage'),
  renderer: 'canvas', loop: false, autoplay: false,
  animationData: ${fs.readFileSync(source, 'utf8')},
  rendererSettings: { clearCanvas: true, preserveAspectRatio: 'xMidYMid meet' }
});
anim.addEventListener('DOMLoaded', function () {
  const frame = document.querySelector('#stage canvas');
  const sheet = document.createElement('canvas');
  sheet.width = ${CELL * COLS};
  sheet.height = ${CELL * ROWS};
  const ctx = sheet.getContext('2d');
  for (let i = 0; i < ${FRAMES}; i++) {
    anim.goToAndStop(i, true);
    ctx.drawImage(frame, (i % ${COLS}) * ${CELL}, Math.floor(i / ${COLS}) * ${CELL}, ${CELL}, ${CELL});
  }
  const out = document.createElement('pre');
  out.textContent = 'SHEET:' + sheet.toDataURL('image/png').split(',')[1] + ':END';
  document.body.appendChild(out);
});
</script>`);

const dom = execFileSync(edge, [
  '--headless=new', '--disable-gpu', '--allow-file-access-from-files',
  '--virtual-time-budget=20000', '--user-data-dir=' + path.join(work, 'profile'),
  '--dump-dom', 'file:///' + page.replace(/\\/g, '/')
], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });

const match = /SHEET:([A-Za-z0-9+/=]+):END/.exec(dom);
if (!match) throw new Error('render produced no sheet');

fs.writeFileSync(output, Buffer.from(match[1], 'base64'));
fs.rmSync(work, { recursive: true, force: true });
console.log('wrote ' + output + ' (' + fs.statSync(output).size + ' bytes)');
